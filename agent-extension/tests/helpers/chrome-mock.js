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
		emit(...args) {
			for (const fn of [...listeners]) fn(...args);
		},
	};
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
		onMessage: createEvent(),
		onStartup: createEvent(),
		onInstalled: createEvent(),
		getManifest() { return JSON.parse(JSON.stringify(manifest)); },
		sendMessage(message, callback) {
			runtime.sent.push(message);
			if (runtime.behavior) return runtime.behavior(message);
			if (callback) callback();
		},
	};

	runtime.respondWith = function (handler) {
		runtime.behavior = function (message) {
			const body = handler(message);
			if (body !== undefined) runtime.onMessage.emit({ id: message.id, body });
		};
	};

	const storage = {
		local: createStorageArea(runtime),
		session: createStorageArea(runtime),
		onChanged: createEvent(),
	};

	const tabs = {
		nextId: 0,
		created: [],
		removed: [],
		messages: [],
		createBehavior: null,
		removeBehavior: null,
		forwardTo: null,
		onRemoved: createEvent(),
		async create(options) {
			tabs.created.push(options);
			if (tabs.createBehavior) return tabs.createBehavior(options);
			return { id: ++tabs.nextId };
		},
		async remove(tabId) {
			tabs.removed.push(tabId);
			if (tabs.removeBehavior) return tabs.removeBehavior(tabId);
		},
		sendMessage(tabId, message, callback) {
			tabs.messages.push({ tabId, message });
			if (tabs.forwardTo) tabs.forwardTo.runtime.onMessage.emit(message);
			if (callback) callback();
		},
	};

	const alarms = {
		created: [],
		map: new Map(),
		getBehavior: null,
		onAlarm: createEvent(),
		async get(name) {
			if (alarms.getBehavior) return alarms.getBehavior(name);
			return alarms.map.get(name) ?? null;
		},
		async create(name, alarmInfo) {
			alarms.created.push({ name, alarmInfo });
			alarms.map.set(name, { name, ...alarmInfo });
		},
	};

	return { runtime, storage, tabs, alarms };
}
