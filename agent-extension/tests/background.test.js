import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh() {
	const env = createEnv({ importScripts: true });
	env.load('controllers/background.js');
	return { env };
}

const tabMessages = env =>
	env.chrome.tabs.messages.map(m => ({ tabId: m.tabId, id: m.message.id, body: jsonOf(m.message.body) }));

const ordersFetches = env =>
	env.fetchStub.calls.filter(c => c.url.includes('decision/orders')).length;

test('the service worker imports its dependencies and creates the orders alarm', async () => {
	const { env } = fresh();
	await env.flush();
	assert.strictEqual(typeof env.grab('CoreMessaging'), 'function');
	assert.strictEqual(typeof env.grab('StorageHandler'), 'function');
	assert.strictEqual(typeof env.grab('TrendCollection'), 'function');
	assert.deepStrictEqual(jsonOf(env.chrome.alarms.created), [
		{ name: 'trend-orders', alarmInfo: { periodInMinutes: 0.5 } },
	]);
});

test('EnsureOrdersAlarm only creates the alarm when it is missing', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.alarms.getBehavior = () => ({ name: 'trend-orders' });
	await env.grab('EnsureOrdersAlarm')();
	assert.strictEqual(env.chrome.alarms.created.length, 1);
	env.chrome.alarms.getBehavior = () => null;
	await env.grab('EnsureOrdersAlarm')();
	assert.strictEqual(env.chrome.alarms.created.length, 2);
});

test('a send message is answered with the reduced body and persists the trend', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ SERVER_URL: 'http://s:9' });
	env.fetchStub.route('decision/take', {
		status: 200,
		body: {
			trend: 'T9',
			commands: [{ action: 'go', params: { url: 'https://n/' } }],
			close_timeout_ms: 111,
		},
	});
	env.chrome.runtime.onMessage.emit(
		{ title: 'send', id: 7, params: { agency: 'A', url: 'u', content: 'c' } },
		{ tab: { id: 3 } }
	);
	await env.settle();
	const take = env.fetchStub.calls.find(c => c.url === 'http://s:9/decision/take');
	assert.ok(take, 'decision/take was not fetched');
	assert.strictEqual(take.data.method, 'POST');
	assert.strictEqual(JSON.parse(take.data.body).trend, null);
	assert.strictEqual(await env.grab('trends').get(3), 'T9');
	assert.deepStrictEqual(tabMessages(env), [{
		tabId: 3,
		id: 7,
		body: {
			commands: [{ action: 'go', params: { url: 'https://n/' } }],
			close_timeout_ms: 111,
		},
	}]);
});

test('a falsy trend in the response removes the stored trend', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ SERVER_URL: 'http://s:9' });
	await env.grab('trends').set(3, 'X');
	env.fetchStub.route('decision/take', {
		status: 200,
		body: { trend: '', commands: [], close_timeout_ms: 1 },
	});
	env.chrome.runtime.onMessage.emit(
		{ title: 'send', id: 2, params: { agency: 'A' } },
		{ tab: { id: 3 } }
	);
	await env.settle();
	assert.strictEqual(await env.grab('trends').get(3), null);
	assert.deepStrictEqual(jsonOf(env.chrome.storage.session.state.get('tab-trends')), {});
	assert.ok(tabMessages(env).some(m => m.id === 2));
});

test('a rejected messaging promise maps to {error:"background", status:0}', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.errors.get = 'boom';
	env.chrome.runtime.onMessage.emit(
		{ title: 'send', id: 3, params: { agency: 'A' } },
		{ tab: { id: 4 } }
	);
	await env.settle();
	assert.deepStrictEqual(tabMessages(env), [{ tabId: 4, id: 3, body: { error: 'background', status: 0 } }]);
});

test('scopes, orders, heartbeat, open-tab and close-tab are routed', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ SERVER_URL: 'http://s:9' });
	env.fetchStub.setDefault({ status: 200, body: {} });

	env.chrome.runtime.onMessage.emit({ title: 'scopes', id: 1 }, { tab: { id: 1 } });
	await env.settle();
	assert.ok(env.fetchStub.calls.some(c =>
		c.url === 'http://s:9/decision/scopes' && c.data.method === 'GET'));
	assert.ok(tabMessages(env).some(m => m.id === 1));

	env.chrome.runtime.onMessage.emit({ title: 'orders', id: 2 }, { tab: { id: 1 } });
	await env.settle();
	assert.ok(env.fetchStub.calls.some(c => c.url === 'http://s:9/decision/orders'));

	env.chrome.runtime.onMessage.emit({ title: 'heartbeat', id: 3 }, { tab: { id: 1 } });
	await env.settle();
	const heartbeat = env.fetchStub.calls.find(c => c.url === 'http://s:9/decision/heartbeat');
	assert.ok(heartbeat);
	assert.strictEqual(JSON.parse(heartbeat.data.body).trend, null);

	env.chrome.runtime.onMessage.emit(
		{ title: 'open-tab', id: 4, params: { url: 'https://new.example/' } },
		{ tab: { id: 1 } }
	);
	await env.settle();
	assert.deepStrictEqual(jsonOf(env.chrome.tabs.created.at(-1)), { url: 'https://new.example/', active: false });
	assert.ok(tabMessages(env).some(m => m.id === 4 && m.body.ok === true));

	env.chrome.runtime.onMessage.emit({ title: 'close-tab', id: 5 }, { tab: { id: 42 } });
	await env.settle();
	assert.deepStrictEqual(jsonOf(env.chrome.tabs.removed), [42]);
	assert.ok(tabMessages(env).some(m => m.id === 5 && m.body.ok === true));
});

test('unknown titles and senders without a tab produce no response', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.runtime.onMessage.emit({ title: 'zzz', id: 9 }, { tab: { id: 1 } });
	env.chrome.runtime.onMessage.emit({ title: 'send', id: 10, params: {} }, {});
	await env.settle();
	assert.strictEqual(env.chrome.tabs.messages.length, 0);
	assert.ok(env.console.entries.some(e => e.args.includes('no tab in sender')));
});

test('tabs.onRemoved drops the trend for that tab', async () => {
	const { env } = fresh();
	await env.flush();
	await env.grab('trends').set(4, 'X');
	env.chrome.tabs.onRemoved.emit(4);
	await env.flush();
	assert.strictEqual(await env.grab('trends').get(4), null);
});

test('CheckNewOrders does nothing while ordering is off', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.alarms.onAlarm.emit({ name: 'trend-orders' });
	await env.settle();
	assert.strictEqual(env.fetchStub.calls.length, 0);
	env.chrome.runtime.onStartup.emit();
	await env.settle();
	assert.strictEqual(env.fetchStub.calls.length, 0);
});

test('storage.onChanged only reacts to ORDERING=true in the local area', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ ORDERING: true });
	env.chrome.storage.onChanged.emit({ API_KEY: { newValue: 'x' } }, 'local');
	await env.settle();
	env.chrome.storage.onChanged.emit({ ORDERING: { newValue: false } }, 'local');
	await env.settle();
	env.chrome.storage.onChanged.emit({ ORDERING: { newValue: true } }, 'sync');
	await env.settle();
	assert.strictEqual(env.fetchStub.calls.length, 0);
	env.chrome.storage.onChanged.emit({ ORDERING: { newValue: true } }, 'local');
	await env.settle();
	assert.strictEqual(env.fetchStub.calls.length, 1);
});

test('onInstalled triggers ResumeOrdering when ordering is on', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ ORDERING: true });
	env.fetchStub.setDefault({ status: 200, body: {} });
	env.chrome.runtime.onInstalled.emit();
	await env.settle();
	assert.strictEqual(ordersFetches(env), 1);
});

test('only the orders alarm name triggers polling', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ ORDERING: true });
	env.chrome.alarms.onAlarm.emit({ name: 'other-alarm' });
	await env.settle();
	assert.strictEqual(env.fetchStub.calls.length, 0);
});

test('CheckNewOrders opens tabs for open commands and records LAST_ORDERS', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ ORDERING: true });
	env.fetchStub.route('decision/orders', {
		status: 200,
		body: { commands: [
			{ action: 'open', params: { url: 'https://a.example/' } },
			null,
			{ action: 'close' },
			{ action: 'fill', object: '#x', params: { value: 'v' } },
			{ action: 'open', params: { url: 'https://b.example/' } },
		] },
	});
	await env.grab('CheckNewOrders')();
	assert.deepStrictEqual(jsonOf(env.chrome.tabs.created), [
		{ url: 'https://a.example/', active: false },
		{ url: 'https://b.example/', active: false },
	]);
	assert.deepStrictEqual(jsonOf(env.chrome.tabs.removed), []);
	assert.ok(env.console.entries.some(e =>
		e.args.includes('unsupported on orders path') && e.args.includes('fill')));
	assert.deepStrictEqual(jsonOf(env.chrome.storage.local.state.get('LAST_ORDERS')), {
		at: env.clock.now(),
		opened: 2,
		error: null,
	});
});

test('CheckNewOrders records fetch errors in LAST_ORDERS', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ ORDERING: true });
	env.fetchStub.route('decision/orders', { status: 500, body: 'oops' });
	await env.grab('CheckNewOrders')();
	assert.deepStrictEqual(jsonOf(env.chrome.storage.local.state.get('LAST_ORDERS')), {
		at: env.clock.now(),
		opened: 0,
		error: 'http',
	});
});

test('CheckNewOrders is reentrancy-guarded while a poll is pending', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.storage.local.set({ ORDERING: true });
	const gate = env.fetchStub.defer();
	env.fetchStub.route('decision/orders', () =>
		gate.promise.then(() => ({ status: 200, body: { commands: [] } })));
	const first = env.grab('CheckNewOrders')();
	await env.flush();
	assert.strictEqual(env.grab('orders_pending'), true);
	await env.grab('CheckNewOrders')();
	assert.strictEqual(ordersFetches(env), 1);
	gate.resolve();
	await first;
	assert.strictEqual(env.grab('orders_pending'), false);
	assert.deepStrictEqual(jsonOf(env.chrome.storage.local.state.get('LAST_ORDERS')), {
		at: env.clock.now(),
		opened: 0,
		error: null,
	});
});

test('OpenTab rejects a missing url with {error:"open-failed"}', async () => {
	const { env } = fresh();
	await env.flush();
	assert.deepStrictEqual(jsonOf(await env.grab('OpenTab')(null)), { error: 'open-failed' });
});

test('OpenTab maps a tabs.create failure to open-failed', async () => {
	const { env } = fresh();
	await env.flush();
	env.chrome.tabs.createBehavior = () => { throw new Error('cannot create'); };
	assert.deepStrictEqual(jsonOf(await env.grab('OpenTab')('https://x/')), { error: 'open-failed' });
});

test('CloseTab resolves ok and maps a tabs.remove failure to close-failed', async () => {
	const { env } = fresh();
	await env.flush();
	assert.deepStrictEqual(jsonOf(await env.grab('CloseTab')(1)), { ok: true });
	env.chrome.tabs.removeBehavior = () => { throw new Error('cannot remove'); };
	assert.deepStrictEqual(jsonOf(await env.grab('CloseTab')(1)), { error: 'close-failed' });
});

test('end-to-end round trip: content BackgroundMessaging.Send through the background to the fetch stub', async () => {
	const contentEnv = createEnv();
	contentEnv.load('controllers/storage-handler.js');
	contentEnv.load('controllers/background-messaging.js');

	const bgEnv = createEnv({ importScripts: true });
	bgEnv.load('controllers/background.js');
	await bgEnv.flush();

	bgEnv.chrome.storage.local.set({ SERVER_URL: 'http://s:9' });
	contentEnv.chrome.runtime.behavior = message => {
		bgEnv.chrome.runtime.onMessage.emit(message, { tab: { id: 12 } });
	};
	bgEnv.chrome.tabs.forwardTo = contentEnv.chrome;

	bgEnv.fetchStub.route('decision/take', {
		status: 200,
		body: { trend: 'E2E', commands: [], close_timeout_ms: 5 },
	});

	const result = await contentEnv.grab('BackgroundMessaging').Send({
		agency: 'A',
		url: 'u',
		content: 'c',
	});
	assert.deepStrictEqual(jsonOf(result), { commands: [], close_timeout_ms: 5 });
	assert.strictEqual(await bgEnv.grab('trends').get(12), 'E2E');
	assert.deepStrictEqual(jsonOf(bgEnv.chrome.storage.session.state.get('tab-trends')), { 12: 'E2E' });
});
