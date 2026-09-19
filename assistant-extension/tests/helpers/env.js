import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import { Window } from 'happy-dom';
import { createChrome, link, deliver } from './chrome-mock.js';

const here = path.dirname(fileURLToPath(import.meta.url));
export const EXTENSION_ROOT = path.resolve(here, '../..');
const CONTROLLERS = path.join(EXTENSION_ROOT, 'controllers');
const realSetImmediate = globalThis.setImmediate;

export function jsonOf(value) {
	return JSON.parse(JSON.stringify(value));
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

	async function deliverFetch(url, data, responder) {
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
		fetch(url, data = {}) {
			const target = String(url);
			calls.push({ url: target, data });
			const found = routes.find(r => matches(r.matcher, target));
			return deliverFetch(target, data, found ? found.responder : defaultResponder);
		},
	};
	return stub;
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
		clear() { entries.length = 0; },
	};
}

export function createEnv(options = {}) {
	const {
		dom = false,
		url = 'https://www.example.com/',
		chrome: chromeOption,
		importScripts = false,
		html = null,
	} = options;

	const chrome = chromeOption ?? createChrome();
	const fetchStub = createFetchStub();
	const consoleMock = createConsoleMock();

	const sandbox = dom ? new Window({ url }) : {};

	sandbox.console = consoleMock;
	sandbox.chrome = chrome;
	sandbox.fetch = fetchStub.fetch;
	sandbox.URL = URL;
	sandbox.URLSearchParams = URLSearchParams;
	sandbox.AbortController = AbortController;
	sandbox.self = sandbox;
	sandbox.prompt = () => null;
	sandbox.setTimeout = setTimeout;
	sandbox.clearTimeout = clearTimeout;

	if (dom) {
		if (sandbox.window !== sandbox) sandbox.window = sandbox;
		if (html !== null) {
			const DOMParserCtor = sandbox.DOMParser;
			sandbox.document = new DOMParserCtor().parseFromString(html, 'text/html');
		}
	}

	const env = {
		sandbox,
		chrome,
		fetchStub,
		console: consoleMock,
		context: null,
		load(relPath) { return loadFile(env, relPath); },
		grab(expression) { return vm.runInContext(expression, env.context); },
		async flush() { return new Promise(resolve => realSetImmediate(resolve)); },
		async settle(times = 12) { for (let i = 0; i < times; i++) await env.flush(); },
	};

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

export { link, deliver };
