import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh(seed) {
	const env = createEnv();
	if (seed !== undefined) env.chrome.storage.session.set({ 'tab-trends': seed });
	env.load('controllers/trend-collection.js');
	const TrendCollection = env.grab('TrendCollection');
	return { env, TrendCollection, tc: new TrendCollection() };
}

test('restores trends preseeded in chrome.storage.session', async () => {
	const { env, tc } = fresh({ 3: 't3', 5: 't5' });
	await tc.ready;
	assert.strictEqual(await tc.get(3), 't3');
	assert.deepStrictEqual(jsonOf(env.chrome.storage.session.state.get('tab-trends')), { 3: 't3', 5: 't5' });
});

test('ops await the restore without awaiting ready explicitly', async () => {
	const { tc } = fresh({ 3: 't3' });
	assert.strictEqual(await tc.get(3), 't3');
});

test('absent storage data yields an empty collection', async () => {
	const { tc } = fresh();
	await tc.ready;
	assert.strictEqual(await tc.get(1), null);
});

test('corrupt stored data is ignored silently', async () => {
	const { env, tc } = fresh('garbage');
	await tc.ready;
	assert.strictEqual(await tc.get(1), null);
	await tc.set(2, 't2');
	assert.deepStrictEqual(jsonOf(env.chrome.storage.session.state.get('tab-trends')), { 2: 't2' });
});

test('set stores the trend and persists it', async () => {
	const { env, tc } = fresh();
	await tc.set(7, 't7');
	assert.strictEqual(await tc.get(7), 't7');
	assert.deepStrictEqual(jsonOf(env.chrome.storage.session.state.get('tab-trends')), { 7: 't7' });
});

test('remove deletes the trend and persists', async () => {
	const { env, tc } = fresh({ 4: 't4' });
	await tc.remove(4);
	assert.strictEqual(await tc.get(4), null);
	assert.deepStrictEqual(jsonOf(env.chrome.storage.session.state.get('tab-trends')), {});
});

test('removing a missing key does not persist', async () => {
	const { env, tc } = fresh({ 4: 't4' });
	await tc.ready;
	const baseline = env.chrome.storage.session.calls.set;
	await tc.remove(9);
	assert.strictEqual(env.chrome.storage.session.calls.set, baseline);
});

test('a missing tab throws "tab is undefined"', async () => {
	const { tc } = fresh();
	await assert.rejects(() => tc.get(undefined), error => error === 'tab is undefined');
	await assert.rejects(() => tc.set(undefined, 'x'), error => error === 'tab is undefined');
	await assert.rejects(() => tc.remove(undefined), error => error === 'tab is undefined');
});
