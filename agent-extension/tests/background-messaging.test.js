import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh() {
	const env = createEnv();
	env.load('controllers/background-messaging.js');
	return { env, BM: env.grab('BackgroundMessaging') };
}

test('each helper sends the right title/params with incrementing ids', async () => {
	const { env, BM } = fresh();
	const results = [
		BM.Send({ a: 1 }),
		BM.Scopes(true),
		BM.Orders(),
		BM.Heartbeat(),
		BM.OpenTab('https://tab.example/'),
		BM.CloseTab(),
	];
	await env.flush();
	assert.deepStrictEqual(jsonOf(env.chrome.runtime.sent), [
		{ title: 'send', params: { a: 1 }, id: 1 },
		{ title: 'scopes', params: { reset: true }, id: 2 },
		{ title: 'orders', id: 3 },
		{ title: 'heartbeat', id: 4 },
		{ title: 'open-tab', params: { url: 'https://tab.example/' }, id: 5 },
		{ title: 'close-tab', id: 6 },
	]);
	await env.advance(45000);
	await Promise.all(results);
});

test('a response with a matching id resolves the pending message', async () => {
	const { env, BM } = fresh();
	const promise = BM.Send({ x: 1 });
	env.chrome.runtime.onMessage.emit({ id: 1, body: { ok: true } });
	assert.deepStrictEqual(jsonOf(await promise), { ok: true });
});

test('responses with unknown ids and falsy bodies are ignored', async () => {
	const { env, BM } = fresh();
	let settled = false;
	const promise = BM.Send({ x: 1 }).then(r => (settled = true, r));
	env.chrome.runtime.onMessage.emit({ id: 99, body: { ok: 'other' } });
	env.chrome.runtime.onMessage.emit({ id: 1, body: null });
	env.chrome.runtime.onMessage.emit({ id: 0, body: { ok: true } });
	await env.flush();
	assert.strictEqual(settled, false);
	env.chrome.runtime.onMessage.emit({ id: 1, body: { ok: true } });
	assert.deepStrictEqual(jsonOf(await promise), { ok: true });
});

test('no response resolves to {error:"no-response", status:0} after the 45s timeout', async () => {
	const { env, BM } = fresh();
	const promise = BM.Send({});
	await env.advance(44999);
	let settled = false;
	promise.then(() => { settled = true; });
	await env.flush();
	assert.strictEqual(settled, false);
	await env.advance(1);
	const result = await promise;
	assert.strictEqual(result.error, 'no-response');
	assert.strictEqual(result.status, 0);
});

test('a late response after the timeout is ignored without side effects', async () => {
	const { env, BM } = fresh();
	const promise = BM.Send({});
	await env.advance(45000);
	const result = await promise;
	env.chrome.runtime.onMessage.emit({ id: 1, body: { ok: true } });
	await env.flush();
	assert.strictEqual(result.error, 'no-response');
});

test('a synchronous sendMessage throw resolves to no-response and cleans the pending entry', async () => {
	const { env, BM } = fresh();
	env.chrome.runtime.behavior = () => { throw new Error('port closed'); };
	const result = await BM.Send({});
	assert.strictEqual(result.error, 'no-response');
	assert.strictEqual(result.status, 0);
	assert.strictEqual(Object.keys(env.grab('BackgroundMessaging.CURRENT_REQUESTS')).length, 0);
});

test('RunListener registers exactly one listener across messages', async () => {
	const { env, BM } = fresh();
	const first = BM.Orders();
	const second = BM.Orders();
	await env.flush();
	assert.strictEqual(env.chrome.runtime.onMessage.listeners.length, 1);
	await env.advance(45000);
	await Promise.all([first, second]);
});

test('runtime.lastError during a response is logged and the response still resolves', async () => {
	const { env, BM } = fresh();
	const promise = BM.Send({});
	env.chrome.runtime.lastError = { message: 'extension context invalidated' };
	env.chrome.runtime.onMessage.emit({ id: 1, body: { ok: true } });
	env.chrome.runtime.lastError = null;
	assert.deepStrictEqual(jsonOf(await promise), { ok: true });
	assert.ok(env.console.entries.some(e => e.args.includes('extension context invalidated')));
});
