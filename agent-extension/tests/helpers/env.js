import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import { Window } from 'happy-dom';
import { createChrome } from './chrome-mock.js';

const here = path.dirname(fileURLToPath(import.meta.url));
export const EXTENSION_ROOT = path.resolve(here, '../..');
const CONTROLLERS = path.join(EXTENSION_ROOT, 'controllers');

const RealDate = globalThis.Date;
const realSetImmediate = globalThis.setImmediate;

export function jsonOf(value) {
	return JSON.parse(JSON.stringify(value));
}

export function createClock(start = 0) {
	let now = start;
	let seq = 0;
	const timers = new Map();

	function schedule(fn, delay, interval) {
		const id = ++seq;
		timers.set(id, { id, due: now + (Number(delay) || 0), fn, interval });
		return id;
	}

	const clock = {
		now() { return now; },
		setTimeout(fn, delay = 0) { return schedule(fn, delay, 0); },
		clearTimeout(id) { timers.delete(id); },
		setInterval(fn, delay = 0) { return schedule(fn, delay, Math.max(1, Number(delay) || 1)); },
		clearInterval(id) { timers.delete(id); },
		pending() { return [...timers.values()].map(t => ({ due: t.due - now, interval: t.interval })); },
		tick(ms) {
			const target = now + ms;
			for (;;) {
				let next = null;
				for (const timer of timers.values()) if (next === null || timer.due < next.due) next = timer;
				if (next === null || next.due > target) break;
				now = next.due;
				if (next.interval) next.due = now + next.interval;
				else timers.delete(next.id);
				next.fn();
			}
			now = target;
		},
	};
	return clock;
}

export function createConsoleMock() {
	const entries = [];
	const capture = level => (...args) => entries.push({ level, args });
	return {
		entries,
		log: capture('log'),
		info: capture('info'),
		warn: capture('warn'),
		error: capture('error'),
		debug: capture('debug'),
		trace: capture('trace'),
		clear() { entries.length = 0; },
	};
}

export function createFetchStub() {
	const calls = [];
	const routes = [];
	let defaultResponder = null;

	function reply(status, body) {
		const text = typeof body === 'string' ? body : JSON.stringify(body ?? {});
		return { ok: status >= 200 && status < 300, status, text: async () => text };
	}

	function hanging(signal) {
		return new Promise((resolve, reject) => {
			const abort = () => reject(Object.assign(new Error('The operation was aborted'), { name: 'AbortError' }));
			if (signal && signal.aborted) return abort();
			if (signal) signal.addEventListener('abort', abort);
		});
	}

	function matches(matcher, url) {
		if (typeof matcher === 'string') return url.includes(matcher);
		if (matcher instanceof RegExp) return matcher.test(url);
		return matcher(url);
	}

	async function deliver(url, data, responder) {
		if (responder == null) return reply(200, {});
		const spec = typeof responder === 'function' ? await responder(url, data) : responder;
		if (spec === 'hang') return hanging(data.signal);
		if (spec && spec.networkError) throw new TypeError('Failed to fetch');
		if (spec && spec.response) return spec.response;
		return reply(spec.status ?? 200, spec.body);
	}

	const stub = {
		calls,
		route(matcher, responder) { routes.push({ matcher, responder }); return stub; },
		setDefault(responder) { defaultResponder = responder; return stub; },
		defer() {
			let resolve, reject;
			const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
			return { promise, resolve, reject };
		},
		fetch(url, data = {}) {
			const target = String(url);
			calls.push({ url: target, data });
			const found = routes.find(r => matches(r.matcher, target));
			return deliver(target, data, found ? found.responder : defaultResponder);
		},
	};
	return stub;
}

function makeFakeDate(clock) {
	return class FakeDate extends RealDate {
		constructor(...args) {
			if (args.length === 0) super(clock.now());
			else super(...args);
		}
		static now() { return clock.now(); }
	};
}

function installFakeLocation(sandbox, url) {
	const parsed = new URL(url);
	const location = {
		href: parsed.href,
		origin: parsed.origin,
		protocol: parsed.protocol,
		host: parsed.host,
		hostname: parsed.hostname,
		port: parsed.port,
		pathname: parsed.pathname,
		search: parsed.search,
		reloadCalls: 0,
		assign(target) { location.href = target; },
		reload() { location.reloadCalls++; },
		toString() { return location.href; },
	};
	Object.defineProperty(sandbox, 'location', { value: location, writable: true, configurable: true });
	return location;
}

export function createEnv(options = {}) {
	const {
		dom = false,
		url = 'https://www.example.com/',
		chrome: chromeOption,
		clock: clockOption,
		importScripts = false,
		html = null,
		autoLoadGate = true,
	} = options;

	const clock = clockOption ?? createClock();
	const chrome = chromeOption ?? createChrome();
	const fetchStub = createFetchStub();
	const consoleMock = createConsoleMock();
	const FakeDate = makeFakeDate(clock);

	const sandbox = dom ? new Window({ url }) : {};

	const env = {
		sandbox,
		chrome,
		clock,
		fetchStub,
		console: consoleMock,
		context: null,
		load(relPath) { return loadFile(env, relPath); },
		grab(expression) { return vm.runInContext(expression, env.context); },
		flush() { return new Promise(resolve => realSetImmediate(resolve)); },
		async settle() { for (let i = 0; i < 8; i++) await env.flush(); },
		async advance(ms) { clock.tick(ms); await env.flush(); },
	};

	sandbox.console = consoleMock;
	sandbox.chrome = chrome;
	sandbox.setTimeout = clock.setTimeout;
	sandbox.clearTimeout = clock.clearTimeout;
	sandbox.setInterval = clock.setInterval;
	sandbox.clearInterval = clock.clearInterval;
	sandbox.Date = FakeDate;
	sandbox.URL = URL;
	sandbox.AbortController = AbortController;
	sandbox.fetch = fetchStub.fetch;

	if (dom) {
		if (sandbox.window !== sandbox) sandbox.window = sandbox;
		installFakeLocation(sandbox, url);
		if (autoLoadGate) {
			sandbox.addEventListener('load', event => event.stopImmediatePropagation());
		}
		if (html !== null) {
			const DOMParserCtor = sandbox.DOMParser;
			sandbox.document = new DOMParserCtor().parseFromString(html, 'text/html');
		}
	}

	if (importScripts) {
		sandbox.importScripts = function (...files) {
			for (const file of files) {
				loadFile(env, path.join(CONTROLLERS, file.replace(/^\.\//, '')));
			}
		};
	}

	env.context = vm.createContext(sandbox);
	return env;
}

function loadFile(env, relPath) {
	const file = path.isAbsolute(relPath) ? relPath : path.resolve(EXTENSION_ROOT, relPath);
	const code = fs.readFileSync(file, 'utf8');
	vm.runInContext(code, env.context, { filename: file });
}
