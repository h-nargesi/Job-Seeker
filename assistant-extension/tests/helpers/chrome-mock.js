import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const manifest = JSON.parse(fs.readFileSync(path.resolve(here, '../../manifest.json'), 'utf8'));

export function createEvent() {
	const listeners = [];
	return {
		listeners,
		addListener(fn) { listeners.push(fn); },
		removeListener(fn) {
			const index = listeners.indexOf(fn);
			if (index >= 0) listeners.splice(index, 1);
		},
		hasListener(fn) { return listeners.includes(fn); },
	};
}

export function dispatch(listeners, message, sender, callback) {
	let responded = false;
	let async = false;

	const sendResponse = response => {
		if (responded) return;
		responded = true;
		if (callback) callback(response);
	};

	for (const fn of [...listeners]) {
		const rv = fn(message, sender, sendResponse);
		if (rv === true) async = true;
	}

	if (!async && !responded && callback) callback(undefined);
}

function createStorageArea(runtime) {
	const area = {
		state: new Map(),
		errors: { get: null, set: null },
		calls: { get: 0, set: 0 },
	};

	function collect(keys) {
		const items = {};
		if (keys === null || keys === undefined) {
			for (const [key, value] of area.state) items[key] = value;
		} else if (typeof keys === 'string') {
			if (area.state.has(keys)) items[keys] = area.state.get(keys);
		} else if (Array.isArray(keys)) {
			for (const key of keys) if (area.state.has(key)) items[key] = area.state.get(key);
		} else {
			for (const key of Object.keys(keys)) {
				items[key] = area.state.has(key) ? area.state.get(key) : keys[key];
			}
		}
		return items;
	}

	function failCallback(callback, message) {
		runtime.lastError = { message };
		try { callback(undefined); } finally { runtime.lastError = null; }
	}

	area.get = function (keys, callback) {
		area.calls.get++;
		if (typeof callback !== 'function') {
			if (area.errors.get) return Promise.reject(area.errors.get);
			return Promise.resolve(collect(keys));
		}
		if (area.errors.get) return failCallback(callback, area.errors.get);
		callback(collect(keys));
	};

	area.set = function (items, callback) {
		area.calls.set++;
		for (const [key, value] of Object.entries(items)) area.state.set(key, value);
		if (typeof callback !== 'function') {
			if (area.errors.set) return Promise.reject(area.errors.set);
			return Promise.resolve();
		}
		if (area.errors.set) {
			failCallback(callback, area.errors.set);
			return;
		}
		callback();
	};

	return area;
}

export function createChrome() {
	const runtime = {
		lastError: null,
		sent: [],
		behavior: null,
		forward: null,
		onMessage: createEvent(),
		getManifest() { return JSON.parse(JSON.stringify(manifest)); },
		sendMessage(message, callback) {
			runtime.sent.push(message);
			if (runtime.forward) {
				dispatch(runtime.forward.runtime.onMessage.listeners, message, { tab: null }, callback);
				return;
			}
			if (runtime.behavior) {
				callback?.(runtime.behavior(message));
				return;
			}
			callback?.(undefined);
		},
	};

	const storage = {
		local: createStorageArea(runtime),
		session: createStorageArea(runtime),
	};
	storage.session.setAccessLevel = function () { };

	const tabs = {
		nextId: 0,
		activeId: 1,
		created: [],
		messages: [],
		forward: null,
		async query() { return [{ id: tabs.activeId }]; },
		async create(options) {
			tabs.created.push(options);
			return { id: ++tabs.nextId };
		},
		sendMessage(tabId, message, callback) {
			tabs.messages.push({ tabId, message });
			if (tabs.forward) {
				dispatch(tabs.forward.runtime.onMessage.listeners, message, { tab: { id: tabId } }, callback);
				return;
			}
			runtime.lastError = { message: 'Could not establish connection. Receiving end does not exist.' };
			try { callback?.(undefined); } finally { runtime.lastError = null; }
		},
	};

	return { runtime, storage, tabs };
}

export function link(swChrome, contentChrome) {
	swChrome.tabs.forward = contentChrome;
	contentChrome.runtime.forward = swChrome;
}

export function deliver(chrome, message) {
	return new Promise(resolve => {
		dispatch(chrome.runtime.onMessage.listeners, message, { tab: null }, resolve);
	});
}
