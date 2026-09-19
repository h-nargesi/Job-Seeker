import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

async function fresh() {
	const env = createEnv({});
	env.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081');
	env.chrome.storage.local.state.set('API_KEY', 'secret-assistant-key');
	env.load('controllers/storage-handler.js');
	env.load('controllers/core-messaging.js');

	const messaging = new (env.grab('CoreMessaging'))();
	return { env, messaging };
}

test('every core request carries the assistant key and X-Client', async () => {
	const { env, messaging } = await fresh();

	await messaging.Jobs();

	const call = env.fetchStub.calls[0];
	assert.strictEqual(call.url, 'https://core.example:8081/assistant/jobs');
	assert.strictEqual(call.data.method, 'GET');
	assert.strictEqual(call.data.headers['X-API-Key'], 'secret-assistant-key');
	assert.strictEqual(call.data.headers['X-Client'], 'assistant');
});

test('applied posts the job id as a query parameter', async () => {
	const { env, messaging } = await fresh();

	await messaging.Applied(42);

	const call = env.fetchStub.calls[0];
	assert.strictEqual(call.url, 'https://core.example:8081/assistant/applied?jobid=42');
	assert.strictEqual(call.data.method, 'POST');
});

test('memory list builds optional scope and confirmed filters', async () => {
	const { env, messaging } = await fresh();

	await messaging.MemoryList();
	await messaging.MemoryList('Apply', true);

	assert.strictEqual(env.fetchStub.calls[0].url, 'https://core.example:8081/assistant/memory');
	assert.strictEqual(env.fetchStub.calls[1].url, 'https://core.example:8081/assistant/memory?scope=Apply&confirmed=true');
});

test('memory mutations target save, edit, confirm, delete and bump routes', async () => {
	const { env, messaging } = await fresh();

	await messaging.MemorySave({ scope: 'Apply', fieldKey: 'email', value: 'x' });
	await messaging.MemoryEdit(7, { value: 'y' });
	await messaging.MemoryConfirm(7, true);
	await messaging.MemoryDelete(7);
	await messaging.MemoryBump(9);

	const urls = jsonOf(env.fetchStub.calls.map(call => call.url));
	assert.deepStrictEqual(urls, [
		'https://core.example:8081/assistant/memorysave',
		'https://core.example:8081/assistant/memoryedit?id=7',
		'https://core.example:8081/assistant/memoryconfirm?id=7&confirmed=true',
		'https://core.example:8081/assistant/memorydelete?id=7',
		'https://core.example:8081/assistant/memorybump?id=9',
	]);
	assert.deepStrictEqual(JSON.parse(env.fetchStub.calls[0].data.body), {
		scope: 'Apply',
		fieldKey: 'email',
		value: 'x',
	});
});

test('the assistant client can never reach the decision automation API', async () => {
	const { env, messaging } = await fresh();

	await messaging.Jobs();
	await messaging.Applied(1);
	await messaging.MemoryList();
	await messaging.MemorySave({});
	await messaging.MemoryBump(1);

	for (const call of env.fetchStub.calls) {
		assert.ok(!call.url.includes('/decision/'), `unexpected decision call: ${call.url}`);
	}
});

test('http and network failures resolve to error objects, never throw', async () => {
	const env = createEnv({});
	env.load('controllers/storage-handler.js');
	env.load('controllers/core-messaging.js');
	const messaging = new (env.grab('CoreMessaging'))();

	let mode = 'http';
	env.fetchStub.route('assistant/jobs', () => mode === 'http'
		? { status: 500, body: { error: 'internal-server-error' } }
		: { networkError: true });

	const http = await messaging.Jobs();
	assert.deepStrictEqual(jsonOf(http), { error: 'internal-server-error', status: 500 });

	mode = 'network';
	const network = await messaging.Jobs();
	assert.deepStrictEqual(jsonOf(network), { error: 'network', status: 0 });
});
