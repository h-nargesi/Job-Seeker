import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh() {
	const env = createEnv({});
	env.load('controllers/storage-handler.js');
	env.load('controllers/logger.js');
	return { env, AssistantLog: env.grab('AssistantLog') };
}

test('entries land in chrome.storage.local and survive a fresh service-worker context', async () => {
	const { env, AssistantLog } = fresh();
	await AssistantLog.Write('info', 'fill', 'start job 12');

	const stored = env.chrome.storage.local.state.get('LOG_BUFFER');
	assert.strictEqual(stored.length, 1);
	assert.strictEqual(stored[0].level, 'info');
	assert.strictEqual(stored[0].where, 'fill');
	assert.strictEqual(stored[0].detail, 'start job 12');
	assert.ok(Number.isFinite(stored[0].t));

	const revived = createEnv({ chrome: env.chrome });
	revived.load('controllers/storage-handler.js');
	revived.load('controllers/logger.js');
	assert.strictEqual((await revived.grab('AssistantLog').All()).length, 1);
});

test('the ring buffer trims at the 500-entry cap', async () => {
	const { env, AssistantLog } = fresh();
	for (let i = 0; i < 505; i++) await AssistantLog.Write('info', 'w', 'e' + i);

	const stored = env.chrome.storage.local.state.get('LOG_BUFFER');
	assert.strictEqual(stored.length, AssistantLog.CAP);
	assert.strictEqual(stored[0].detail, 'e5');
	assert.strictEqual(stored.at(-1).detail, 'e504');
});

test('the worst-case buffer stays within the ~175 KB budget', async () => {
	const { env, AssistantLog } = fresh();
	for (let i = 0; i < AssistantLog.CAP; i++)
		await AssistantLog.Write('error', 'w'.repeat(1000), 'x'.repeat(5000));

	const stored = env.chrome.storage.local.state.get('LOG_BUFFER');
	assert.strictEqual(stored.length, AssistantLog.CAP);
	for (const entry of stored) {
		assert.ok(entry.detail.length <= 200);
		assert.ok(entry.where.length <= 60);
	}
	assert.ok(JSON.stringify(stored).length < 175 * 1024);
});

test('clear empties the buffer', async () => {
	const { env, AssistantLog } = fresh();
	await AssistantLog.Write('warn', 'tab', 'tab-timeout');
	await AssistantLog.Write('error', 'llm', 'llm-http-500');
	await AssistantLog.Clear();

	assert.deepStrictEqual(jsonOf(await AssistantLog.All()), []);
	assert.deepStrictEqual(jsonOf(env.chrome.storage.local.state.get('LOG_BUFFER')), []);
});

test('details are truncated, non-strings stringified and levels coerced', async () => {
	const { env, AssistantLog } = fresh();
	await AssistantLog.Write('bogus', 'w', 'y'.repeat(1000));
	await AssistantLog.Write('error', 'w', { code: 'llm-http-500' });

	const stored = env.chrome.storage.local.state.get('LOG_BUFFER');
	assert.strictEqual(stored[0].detail.length, 200);
	assert.strictEqual(stored[0].level, 'info');
	assert.strictEqual(stored[1].level, 'error');
	assert.strictEqual(stored[1].detail, '{"code":"llm-http-500"}');
});
