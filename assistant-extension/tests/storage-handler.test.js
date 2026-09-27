import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv } from './helpers/env.js';

function fresh() {
	const env = createEnv();
	env.load('controllers/storage-handler.js');
	return env;
}

async function llamaUrl(env, stored) {
	if (stored !== undefined) env.chrome.storage.local.state.set('LLAMA_URL', stored);
	const promise = env.grab('StorageHandler.LlamaUrlAsync()');
	await env.flush();
	return promise;
}

test('llama url falls back to the default base', async () => {
	const env = fresh();
	assert.strictEqual(await llamaUrl(env, undefined), 'http://localhost:8081/');
});

test('llama url gets a trailing slash', async () => {
	const env = fresh();
	assert.strictEqual(await llamaUrl(env, 'http://localhost:8081'), 'http://localhost:8081/');
});

test('worker-style /v1 base is normalized to the plain base', async () => {
	const env = fresh();
	assert.strictEqual(await llamaUrl(env, 'http://localhost:8081/v1'), 'http://localhost:8081/');
	assert.strictEqual(await llamaUrl(env, 'http://localhost:8081/v1/'), 'http://localhost:8081/');
});

test('path prefixes keep their /v1 tail stripped only once', async () => {
	const env = fresh();
	assert.strictEqual(await llamaUrl(env, 'http://gpu-box:9000/llama/v1/'), 'http://gpu-box:9000/llama/');
	assert.strictEqual(await llamaUrl(env, 'http://gpu-box:9000/llama/'), 'http://gpu-box:9000/llama/');
});

test('llm client posts to /v1/chat/completions even with a worker-style stored url', async () => {
	const env = fresh();
	env.chrome.storage.local.state.set('LLAMA_URL', 'http://localhost:8081/v1');
	env.chrome.storage.local.state.set('LLAMA_MODEL', 'test-model');
	env.load('controllers/logger.js');
	env.load('controllers/llm-client.js');
	env.fetchStub.route('v1/chat/completions', {
		body: { choices: [{ message: { content: 'ok' } }] },
	});

	const promise = env.grab('LlmClient.Create().then(function (client) { return client.Chat([]); })');
	await env.settle();
	const result = await promise;

	assert.strictEqual(result.error, undefined);
	assert.strictEqual(result.content, 'ok');
	const call = env.fetchStub.calls.find(c => c.url.includes('v1/chat/completions'));
	assert.ok(call);
	assert.strictEqual(call.url, 'http://localhost:8081/v1/chat/completions');
});
