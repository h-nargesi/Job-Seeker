import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv } from './helpers/env.js';

function fresh() {
	const env = createEnv();
	env.load('controllers/storage-handler.js');
	return { env, StorageHandler: env.grab('StorageHandler') };
}

test('Get returns the stored value', async () => {
	const { env, StorageHandler } = fresh();
	env.chrome.storage.local.set({ K: 'v' });
	assert.strictEqual(await StorageHandler.Get('K', 'default'), 'v');
});

test('Get falls back to the default when the key is missing', async () => {
	const { StorageHandler } = fresh();
	assert.strictEqual(await StorageHandler.Get('MISSING', 'default'), 'default');
	assert.strictEqual(await StorageHandler.Get('MISSING'), undefined);
});

test('Get rejects when runtime.lastError is set', async () => {
	const { env, StorageHandler } = fresh();
	env.chrome.storage.local.errors.get = 'storage is corrupt';
	await assert.rejects(
		StorageHandler.Get('K', 'd'),
		error => error === 'storage is corrupt'
	);
});

test('Set writes a {key: value} object', async () => {
	const { env, StorageHandler } = fresh();
	await StorageHandler.Set('K', 'x');
	assert.strictEqual(env.chrome.storage.local.state.get('K'), 'x');
});

test('ServerUrlAsync returns the default URL', async () => {
	const { StorageHandler } = fresh();
	assert.strictEqual(await StorageHandler.ServerUrlAsync(), 'http://localhost:8081/');
});

test('ServerUrlAsync returns the stored URL coerced to string', async () => {
	const { env, StorageHandler } = fresh();
	env.chrome.storage.local.set({ SERVER_URL: 123 });
	assert.strictEqual(await StorageHandler.ServerUrlAsync(), '123');
});

test('set ServerUrl persists the raw value', async () => {
	const { env, StorageHandler } = fresh();
	StorageHandler.ServerUrl = 'http://a.example/';
	assert.strictEqual(env.chrome.storage.local.state.get('SERVER_URL'), 'http://a.example/');
});

test('ApiKeyAsync returns empty string by default and the stored key otherwise', async () => {
	const { env, StorageHandler } = fresh();
	assert.strictEqual(await StorageHandler.ApiKeyAsync(), '');
	env.chrome.storage.local.set({ API_KEY: 'secret' });
	assert.strictEqual(await StorageHandler.ApiKeyAsync(), 'secret');
});

test('set ApiKey persists the value', async () => {
	const { env, StorageHandler } = fresh();
	StorageHandler.ApiKey = 'k';
	assert.strictEqual(env.chrome.storage.local.state.get('API_KEY'), 'k');
});

test('OrderingAsync defaults to false and coerces to boolean', async () => {
	const { env, StorageHandler } = fresh();
	assert.strictEqual(await StorageHandler.OrderingAsync(), false);
	env.chrome.storage.local.set({ ORDERING: 'yes' });
	assert.strictEqual(await StorageHandler.OrderingAsync(), true);
});

test('set Ordering coerces the value to boolean', async () => {
	const { env, StorageHandler } = fresh();
	StorageHandler.Ordering = 'yes';
	assert.strictEqual(env.chrome.storage.local.state.get('ORDERING'), true);
});

test('LastOrdersAsync defaults to null and returns the stored object', async () => {
	const { env, StorageHandler } = fresh();
	assert.strictEqual(await StorageHandler.LastOrdersAsync(), null);
	const stored = { at: 5, opened: 1, error: null };
	env.chrome.storage.local.set({ LAST_ORDERS: stored });
	assert.strictEqual(await StorageHandler.LastOrdersAsync(), stored);
});

test('key constants keep their storage names', () => {
	const { StorageHandler } = fresh();
	assert.strictEqual(StorageHandler.SERVER_URL, 'SERVER_URL');
	assert.strictEqual(StorageHandler.API_KEY, 'API_KEY');
	assert.strictEqual(StorageHandler.ORDERING, 'ORDERING');
	assert.strictEqual(StorageHandler.LAST_ORDERS, 'LAST_ORDERS');
});
