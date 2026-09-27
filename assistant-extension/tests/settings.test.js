import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createEnv, jsonOf, EXTENSION_ROOT } from './helpers/env.js';

function settingsBodyHtml() {
	const html = fs.readFileSync(path.join(EXTENSION_ROOT, 'application/settings.html'), 'utf8');
	const body = html.match(/<body>([\s\S]*)<\/body>/)[1];
	return body.replace(/<script[\s\S]*?<\/script>/g, '');
}

function fresh(logs = [], theme = undefined) {
	const env = createEnv({ dom: true, url: 'chrome-extension://settings/index.html' });
	env.sandbox.document.body.innerHTML = settingsBodyHtml();

	env.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081/');
	env.chrome.storage.local.state.set('LLAMA_MODEL', 'test-model');
	if (theme !== undefined) env.chrome.storage.local.state.set('THEME', theme);
	if (logs.length) env.chrome.storage.local.state.set('LOG_BUFFER', logs);

	env.load('controllers/storage-handler.js');
	env.load('controllers/logger.js');
	env.load('application/theme.js');
	env.load('application/settings.js');
	return env;
}

const $ = (env, id) => env.sandbox.document.getElementById(id);

async function settle(env) {
	for (let i = 0; i < 12; i++) await env.flush();
}

function press(env, element, keyCode) {
	const event = new env.sandbox.window.KeyboardEvent('keyup', { bubbles: true });
	Object.defineProperty(event, 'keyCode', { value: keyCode });
	element.dispatchEvent(event);
}

test('loads the stored settings values into the popup inputs', async () => {
	const env = fresh();
	await settle(env);

	assert.strictEqual($(env, 'ServerUrl').value, 'https://core.example:8081/');
	assert.strictEqual($(env, 'LlamaModel').value, 'test-model');
	assert.strictEqual($(env, 'ApiKey').value, '');
});

test('Enter saves a settings field, other keys do not', async () => {
	const env = fresh();
	await settle(env);

	$(env, 'LlamaUrl').value = 'http://llama.example:9000';
	press(env, $(env, 'LlamaUrl'), 9);
	assert.strictEqual(env.chrome.storage.local.state.get('LLAMA_URL'), undefined);

	press(env, $(env, 'LlamaUrl'), 13);
	assert.strictEqual(env.chrome.storage.local.state.get('LLAMA_URL'), 'http://llama.example:9000');
});

test('the theme select loads the stored mode and saves on change', async () => {
	const env = fresh([], 'dark');
	await settle(env);

	assert.strictEqual($(env, 'ThemeMode').value, 'dark');
	assert.strictEqual(env.sandbox.document.documentElement.getAttribute('data-bs-theme'), 'dark');

	$(env, 'ThemeMode').value = 'system';
	$(env, 'ThemeMode').dispatchEvent(new env.sandbox.window.Event('change'));
	await settle(env);

	assert.strictEqual(env.chrome.storage.local.state.get('THEME'), 'system');
	assert.strictEqual(env.sandbox.document.documentElement.getAttribute('data-bs-theme'), 'light');
});

test('the open-panel button opens the side panel on the active tab', async () => {
	const env = fresh();
	await settle(env);

	$(env, 'OpenPanel').click();
	await settle(env);

	assert.deepStrictEqual(jsonOf(env.chrome.sidePanel.opened), [{ tabId: 1 }]);
});

test('logs render newest-first with the level filter and the 100-entry cap', async () => {
	const entries = [];
	for (let i = 0; i < 130; i++) {
		entries.push({ t: 1700000000000 + i * 1000, level: i % 10 === 0 ? 'error' : 'info', where: 'fill', detail: 'entry ' + i });
	}
	const env = fresh(entries);
	await settle(env);

	const rows = Array.from($(env, 'LogsList').querySelectorAll('div'));
	assert.strictEqual(rows.length, 100);
	assert.ok(rows[0].textContent.includes('entry 129'));
	assert.ok(rows.at(-1).textContent.includes('entry 30'));
	assert.ok(rows.some(row => row.textContent.includes(' error ')));

	$(env, 'LogsFilter').value = 'error';
	$(env, 'LogsFilter').dispatchEvent(new env.sandbox.window.Event('change'));
	await settle(env);

	const filtered = Array.from($(env, 'LogsList').querySelectorAll('div'));
	assert.ok(filtered.length > 0);
	assert.ok(filtered.every(row => row.textContent.includes(' error ')));
});

test('clear empties the log store and the view', async () => {
	const env = fresh([{ t: 1700000000000, level: 'info', where: 'fill', detail: 'start' }]);
	await settle(env);

	$(env, 'LogsClear').click();
	await settle(env);

	assert.deepStrictEqual(jsonOf(env.chrome.storage.local.state.get('LOG_BUFFER')), []);
	assert.strictEqual($(env, 'LogsList').querySelectorAll('div').length, 0);
});

test('download exports the buffer as a dated .log file from a blob', async () => {
	const created = [];
	const downloads = [];

	const env = fresh([
		{ t: 1700000000000, level: 'info', where: 'fill', detail: 'start job 12' },
		{ t: 1700000001000, level: 'error', where: 'llm', detail: 'llm-http-500' },
	]);
	env.sandbox.URL.createObjectURL = blob => { created.push(blob); return 'blob:fake'; };
	env.sandbox.URL.revokeObjectURL = () => { };
	env.sandbox.window.HTMLAnchorElement.prototype.click = function () {
		downloads.push({ name: this.download, href: this.href });
	};
	await settle(env);

	$(env, 'LogsDownload').click();
	await settle(env);

	assert.strictEqual(downloads.length, 1);
	assert.match(downloads[0].name, /^assistant-\d{8}\.log$/);
	assert.strictEqual(downloads[0].href, 'blob:fake');

	const text = await created[0].text();
	assert.ok(text.includes('info fill: start job 12'));
	assert.ok(text.includes('error llm: llm-http-500'));
});
