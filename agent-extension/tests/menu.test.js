import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { createEnv } from './helpers/env.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const menuHtml = fs.readFileSync(path.resolve(here, '../application/menu.html'), 'utf8');

function fresh(seed = {}) {
	const env = createEnv({
		dom: true,
		url: 'chrome-extension://abc/application/menu.html',
		html: menuHtml,
	});
	for (const [key, value] of Object.entries(seed)) {
		env.chrome.storage.local.set({ [key]: value });
	}
	env.load('controllers/storage-handler.js');

	const capture = {};
	for (const id of ['ServerUrl', 'ApiKey', 'OpenServer', 'Ordering']) {
		const element = env.sandbox.document.getElementById(id);
		const original = element.addEventListener.bind(element);
		element.addEventListener = (type, handler) => {
			capture[id] = capture[id] ?? {};
			capture[id][type] = handler;
			return original(type, handler);
		};
	}

	env.load('application/menu.js');
	return { env, capture, doc: env.sandbox.document };
}

const keyup13 = () => ({ preventDefault() {}, keyCode: 13 });
const keyup12 = () => ({ preventDefault() {}, keyCode: 12 });

test('LoadData populates fields from storage and the manifest', async () => {
	const { env, doc } = fresh({
		SERVER_URL: 'http://s:9/',
		API_KEY: 'k1',
		ORDERING: true,
		LAST_ORDERS: { at: 1000, opened: 2, error: null },
	});
	await env.settle();
	assert.strictEqual(doc.getElementById('ServerUrl').value, 'http://s:9/');
	assert.strictEqual(doc.getElementById('ApiKey').value, 'k1');
	assert.strictEqual(doc.getElementById('Ordering').getAttribute('data-state'), 'on');
	assert.strictEqual(doc.getElementById('ManifestTitle').innerText, 'Job Seeker Agent');
	assert.ok(doc.getElementById('ManifestDescr').innerText.includes('job agents'));
	const expected = `Last poll ${new Date(1000).toLocaleTimeString()} — opened 2`;
	assert.strictEqual(doc.getElementById('OrdersStatus').innerText, expected);
});

test('defaults render when nothing is stored', async () => {
	const { env, doc } = fresh();
	await env.settle();
	assert.strictEqual(doc.getElementById('ServerUrl').value, 'http://localhost:8081/');
	assert.strictEqual(doc.getElementById('ApiKey').value, '');
	assert.strictEqual(doc.getElementById('Ordering').getAttribute('data-state'), 'off');
	assert.strictEqual(doc.getElementById('OrdersStatus').innerText, 'No orders poll yet.');
});

test('Enter on ServerUrl trims and strips all trailing slashes before saving', async () => {
	const { env, doc, capture } = fresh();
	await env.settle();
	doc.getElementById('ServerUrl').value = '  http://x.example// ';
	capture.ServerUrl.keyup(keyup13());
	assert.strictEqual(doc.getElementById('ServerUrl').value, 'http://x.example');
	assert.strictEqual(env.chrome.storage.local.state.get('SERVER_URL'), 'http://x.example');
});

test('other keys on ServerUrl do not save', async () => {
	const { env, doc, capture } = fresh();
	await env.settle();
	doc.getElementById('ServerUrl').value = 'http://keep/';
	capture.ServerUrl.keyup(keyup12());
	assert.strictEqual(doc.getElementById('ServerUrl').value, 'http://keep/');
	assert.strictEqual(env.chrome.storage.local.state.get('SERVER_URL'), undefined);
});

test('Enter on an empty ServerUrl stores an empty string', async () => {
	const { env, doc, capture } = fresh();
	await env.settle();
	doc.getElementById('ServerUrl').value = '';
	capture.ServerUrl.keyup(keyup13());
	assert.strictEqual(env.chrome.storage.local.state.get('SERVER_URL'), '');
});

test('Enter on ApiKey trims and saves; empty becomes ""', async () => {
	const { env, doc, capture } = fresh();
	await env.settle();
	doc.getElementById('ApiKey').value = '  k  ';
	capture.ApiKey.keyup(keyup13());
	assert.strictEqual(env.chrome.storage.local.state.get('API_KEY'), 'k');
	doc.getElementById('ApiKey').value = '';
	capture.ApiKey.keyup(keyup13());
	assert.strictEqual(env.chrome.storage.local.state.get('API_KEY'), '');
});

test('other keys on ApiKey do not save', async () => {
	const { env, doc, capture } = fresh();
	await env.settle();
	doc.getElementById('ApiKey').value = 'secret';
	capture.ApiKey.keyup(keyup12());
	assert.strictEqual(env.chrome.storage.local.state.get('API_KEY'), undefined);
});

test('clicking Ordering toggles the stored flag and the data-state attribute', async () => {
	const { env, doc, capture } = fresh();
	await env.settle();
	await capture.Ordering.click();
	assert.strictEqual(env.chrome.storage.local.state.get('ORDERING'), true);
	assert.strictEqual(doc.getElementById('Ordering').getAttribute('data-state'), 'on');
	await capture.Ordering.click();
	assert.strictEqual(env.chrome.storage.local.state.get('ORDERING'), false);
	assert.strictEqual(doc.getElementById('Ordering').getAttribute('data-state'), 'off');
});

test('OpenServer opens the stored server URL', async () => {
	const { env, capture } = fresh({ SERVER_URL: 'http://s:1/' });
	await env.settle();
	let opened = null;
	env.sandbox.open = url => { opened = url; return null; };
	await capture.OpenServer.click();
	assert.strictEqual(opened, 'http://s:1/');
});

test('LoadOrdersStatus renders the error variant', async () => {
	const { env, doc } = fresh({ LAST_ORDERS: { at: 2000, opened: 0, error: 'http' } });
	await env.settle();
	const text = doc.getElementById('OrdersStatus').innerText;
	assert.ok(text.includes('error: http'));
	assert.ok(text.includes('Last poll'));
});
