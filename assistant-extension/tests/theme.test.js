import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv } from './helpers/env.js';

function mediaStub(dark = false) {
	const media = {
		matches: dark,
		listeners: [],
		addEventListener(type, fn) { media.listeners.push(fn); },
		removeEventListener() { },
		flip(value) {
			media.matches = value;
			for (const fn of [...media.listeners]) fn({ matches: value });
		},
	};
	return media;
}

function fresh(theme = undefined, media = null) {
	const env = createEnv({ dom: true, url: 'chrome-extension://settings/index.html' });
	if (media) env.sandbox.matchMedia = () => media;
	if (theme !== undefined) env.chrome.storage.local.state.set('THEME', theme);
	env.load('controllers/storage-handler.js');
	env.load('application/theme.js');
	return env;
}

const themeOf = env => env.sandbox.document.documentElement.getAttribute('data-bs-theme');

async function settle(env) {
	for (let i = 0; i < 12; i++) await env.flush();
}

test('default is system, resolved to light on a light OS', async () => {
	const env = fresh();
	await settle(env);

	assert.strictEqual(themeOf(env), 'light');
	assert.strictEqual(env.grab('ThemeHandler').mode, 'system');
});

test('a stored dark mode overrides the light system preference', async () => {
	const env = fresh('dark');
	await settle(env);

	assert.strictEqual(themeOf(env), 'dark');
});

test('system mode follows the OS preference', async () => {
	const env = fresh('system', mediaStub(true));
	await settle(env);

	assert.strictEqual(themeOf(env), 'dark');
});

test('an invalid stored value falls back to system/light', async () => {
	const env = fresh('neon');
	await settle(env);

	assert.strictEqual(themeOf(env), 'light');
	assert.strictEqual(env.grab('ThemeHandler').mode, 'system');
});

test('Set persists the mode and applies it', async () => {
	const env = fresh();
	await settle(env);

	await env.grab('ThemeHandler').Set('dark');
	await settle(env);

	assert.strictEqual(env.chrome.storage.local.state.get('THEME'), 'dark');
	assert.strictEqual(themeOf(env), 'dark');
});

test('a storage change from another page re-applies the theme', async () => {
	const env = fresh();
	await settle(env);

	await env.chrome.storage.local.set({ THEME: 'dark' });
	await settle(env);

	assert.strictEqual(themeOf(env), 'dark');

	await env.chrome.storage.local.set({ THEME: 'light' });
	await settle(env);

	assert.strictEqual(themeOf(env), 'light');
});

test('an OS preference flip only re-applies while in system mode', async () => {
	const media = mediaStub(false);
	const env = fresh('system', media);
	await settle(env);

	assert.strictEqual(themeOf(env), 'light');

	media.flip(true);
	assert.strictEqual(themeOf(env), 'dark');

	await env.grab('ThemeHandler').Set('light');
	media.flip(true);
	assert.strictEqual(themeOf(env), 'light');
});

test('a bound select loads the stored mode, saves on change, and mirrors live changes', async () => {
	const env = fresh('dark');
	await settle(env);

	const doc = env.sandbox.document;
	const select = doc.createElement('select');
	select.id = 'ThemeMode';
	for (const mode of ['system', 'light', 'dark']) {
		const option = doc.createElement('option');
		option.value = mode;
		select.appendChild(option);
	}
	doc.body.appendChild(select);

	await env.grab('ThemeHandler').BindSelect(select);
	await settle(env);

	assert.strictEqual(select.value, 'dark');

	select.value = 'light';
	select.dispatchEvent(new env.sandbox.window.Event('change'));
	await settle(env);

	assert.strictEqual(env.chrome.storage.local.state.get('THEME'), 'light');
	assert.strictEqual(themeOf(env), 'light');

	await env.chrome.storage.local.set({ THEME: 'dark' });
	await settle(env);

	assert.strictEqual(select.value, 'dark');
});
