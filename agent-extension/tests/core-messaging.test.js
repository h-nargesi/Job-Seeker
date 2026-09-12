import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh() {
	const env = createEnv();
	env.load('controllers/storage-handler.js');
	env.load('controllers/core-messaging.js');
	const CoreMessaging = env.grab('CoreMessaging');
	return { env, CoreMessaging, cm: new CoreMessaging() };
}

const scopeFetchCount = env =>
	env.fetchStub.calls.filter(c => c.url.includes('decision/scopes')).length;

const BASE_HEADERS = {
	Accept: 'application/json',
	'Content-Type': 'application/json',
	'X-Client': 'search',
};

test('CheckServerUrl appends the missing trailing slash', async () => {
	const { env, cm } = fresh();
	env.chrome.storage.local.set({ SERVER_URL: 'http://localhost:8081' });
	assert.strictEqual(await cm.CheckServerUrl(), 'http://localhost:8081/');
});

test('CheckServerUrl caches the URL after the first storage read', async () => {
	const { env, cm } = fresh();
	env.chrome.storage.local.set({ SERVER_URL: 'http://a.example' });
	assert.strictEqual(await cm.CheckServerUrl(), 'http://a.example/');
	env.chrome.storage.local.set({ SERVER_URL: 'http://b.example/' });
	assert.strictEqual(await cm.CheckServerUrl(), 'http://a.example/');
	assert.strictEqual(env.chrome.storage.local.calls.get, 1);
});

test('BuildHeaders omits X-API-Key when the key is empty and keeps the base headers pristine', async () => {
	const { env, cm, CoreMessaging } = fresh();
	const headers = await cm.BuildHeaders();
	assert.deepStrictEqual(jsonOf(headers), BASE_HEADERS);
	assert.strictEqual('X-API-Key' in headers, false);
	assert.strictEqual('X-API-Key' in CoreMessaging.HEADERS, false);
});

test('BuildHeaders adds X-API-Key when a key is stored', async () => {
	const { env, cm } = fresh();
	env.chrome.storage.local.set({ API_KEY: 'secret' });
	const headers = await cm.BuildHeaders();
	assert.strictEqual(headers['X-API-Key'], 'secret');
	assert.deepStrictEqual(jsonOf(headers), { ...BASE_HEADERS, 'X-API-Key': 'secret' });
});

test('FetchJson parses a successful JSON response', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision', { status: 200, body: { ok: true, trend: 'T1' } });
	const result = await cm.FetchJson('http://x/decision', {});
	assert.deepStrictEqual(jsonOf(result), { ok: true, trend: 'T1' });
});

test('FetchJson maps a non-ok JSON error body to {error, status}', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision', { status: 404, body: { error: 'not-found' } });
	const result = await cm.FetchJson('http://x/decision', {});
	assert.strictEqual(result.error, 'not-found');
	assert.strictEqual(result.status, 404);
});

test('FetchJson maps a non-ok non-JSON body to error "http"', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision', { status: 502, body: 'Bad gateway' });
	const result = await cm.FetchJson('http://x/decision', {});
	assert.strictEqual(result.error, 'http');
	assert.strictEqual(result.status, 502);
});

test('FetchJson maps invalid JSON on a 200 response to "invalid-json"', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision', { status: 200, body: 'not json' });
	const result = await cm.FetchJson('http://x/decision', {});
	assert.strictEqual(result.error, 'invalid-json');
	assert.strictEqual(result.status, 200);
});

test('FetchJson maps a rejected fetch to "network"', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision', () => { throw new TypeError('Failed to fetch'); });
	const result = await cm.FetchJson('http://x/decision', {});
	assert.strictEqual(result.error, 'network');
	assert.strictEqual(result.status, 0);
});

test('FetchJson aborts as "timeout" exactly at REQUEST_TIMEOUT and not before', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('slow', 'hang');
	let result = null;
	const promise = cm.FetchJson('http://x/slow', {}).then(r => { result = r; });
	await env.flush();
	await env.advance(29999);
	assert.strictEqual(result, null);
	await env.advance(1);
	assert.strictEqual(result.error, 'timeout');
	assert.strictEqual(result.status, 0);
});

test('FetchJson clears the abort timer once the response arrives', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('fast', { status: 200, body: {} });
	await cm.FetchJson('http://x/fast', {});
	await env.advance(120000);
	assert.strictEqual(env.clock.pending().length, 0);
});

test('Send posts the params to decision/take with JSON headers', async () => {
	const { env, cm } = fresh();
	env.chrome.storage.local.set({ SERVER_URL: 'http://s:9' });
	env.fetchStub.route('decision/take', {
		status: 200,
		body: { commands: [{ action: 'recheck' }] },
	});
	const result = await cm.Send({ agency: 'A', url: 'u', content: 'c' });
	const call = env.fetchStub.calls.find(c => c.url === 'http://s:9/decision/take');
	assert.ok(call, 'decision/take was not fetched');
	assert.strictEqual(call.data.method, 'POST');
	assert.strictEqual(call.data.body, JSON.stringify({ agency: 'A', url: 'u', content: 'c' }));
	assert.deepStrictEqual(jsonOf(call.data.headers), BASE_HEADERS);
	assert.deepStrictEqual(jsonOf(result), { commands: [{ action: 'recheck' }] });
});

test('Heartbeat posts the params to decision/heartbeat', async () => {
	const { env, cm } = fresh();
	env.chrome.storage.local.set({ SERVER_URL: 'http://s:9' });
	env.fetchStub.route('decision/heartbeat', { status: 200, body: { ok: true } });
	await cm.Heartbeat({ trend: 'T' });
	const call = env.fetchStub.calls.find(c => c.url === 'http://s:9/decision/heartbeat');
	assert.ok(call, 'decision/heartbeat was not fetched');
	assert.strictEqual(call.data.method, 'POST');
	assert.strictEqual(call.data.body, JSON.stringify({ trend: 'T' }));
});

test('Scopes fetches GET decision/scopes and caches within the 60s TTL', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision/scopes', {
		status: 200,
		body: [{ name: 'A', domain: 'a\\.com' }],
	});
	const first = await cm.Scopes();
	assert.strictEqual(jsonOf(first)[0].name, 'A');
	assert.strictEqual(scopeFetchCount(env), 1);
	await cm.Scopes();
	assert.strictEqual(scopeFetchCount(env), 1);
	await env.advance(59999);
	await cm.Scopes();
	assert.strictEqual(scopeFetchCount(env), 1);
	await env.advance(1);
	await cm.Scopes();
	assert.strictEqual(scopeFetchCount(env), 2);
	const call = env.fetchStub.calls.at(-1);
	assert.strictEqual(call.data.method, 'GET');
	assert.strictEqual(call.url, 'http://localhost:8081/decision/scopes');
});

test('Scopes does not cache error results', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision/scopes', { status: 500, body: 'oops' });
	const result = await cm.Scopes();
	assert.strictEqual(result.error, 'http');
	await cm.Scopes();
	assert.strictEqual(scopeFetchCount(env), 2);
	assert.strictEqual(env.grab('CoreMessaging.SCOPES'), undefined);
});

test('Scopes maps a sync throw to {error:"client", status:0}', async () => {
	const { env, cm } = fresh();
	env.chrome.storage.local.errors.get = 'boom';
	const result = await cm.Scopes();
	assert.strictEqual(result.error, 'client');
	assert.strictEqual(result.status, 0);
});

test('Orders fetches GET decision/orders', async () => {
	const { env, cm } = fresh();
	env.fetchStub.route('decision/orders', { status: 200, body: { commands: [] } });
	const result = await cm.Orders();
	assert.deepStrictEqual(jsonOf(result), { commands: [] });
	const call = env.fetchStub.calls.find(c => c.url.includes('decision/orders'));
	assert.ok(call);
	assert.strictEqual(call.data.method, 'GET');
});

test('Orders maps a sync throw to {error:"client", status:0}', async () => {
	const { env, cm } = fresh();
	env.chrome.storage.local.errors.get = 'boom';
	const result = await cm.Orders();
	assert.strictEqual(result.error, 'client');
	assert.strictEqual(result.status, 0);
});
