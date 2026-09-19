import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

async function fresh() {
	const env = createEnv({ dom: true, url: 'https://ats.example/apply' });
	env.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081/');
	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/form-inventory.js');
	env.load('controllers/fill-handler.js');
	env.load('controllers/submit-diff.js');
	env.load('controllers/assistant-page.js');
	env.chrome.runtime.behavior = () => ({ flushed: 0 });
	return env;
}

function submit(env) {
	const form = env.sandbox.document.getElementById('f');
	form.dispatchEvent(new env.sandbox.window.Event('submit', { bubbles: true }));
}

test('a human submit queues corrections for fields the human changed after the fill', async () => {
	const env = await fresh();
	env.sandbox.document.body.innerHTML = `
		<form id="f">
			<input type="text" name="email" value="">
		</form>
	`;

	env.grab('FormInventory').Extract();
	env.grab('FillHandler').Apply('f1', 'ai@example.com');
	env.sandbox.document.querySelector('input').value = 'human@example.com';

	submit(env);
	await env.settle();

	const queued = jsonOf(env.chrome.storage.session.state.get('PENDING_DIFFS'));
	assert.strictEqual(queued.length, 1);
	assert.deepStrictEqual(queued[0], {
		domain: 'ats.example',
		fieldKey: 'email',
		fieldLabel: '',
		aiValue: 'ai@example.com',
		finalValue: 'human@example.com',
	});

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'flush-diffs'));
});

test('a submit with no assistant-filled fields writes nothing', async () => {
	const env = await fresh();
	env.sandbox.document.body.innerHTML = '<form id="f"><input type="text" name="email"></form>';

	submit(env);
	await env.settle();

	assert.ok(!env.chrome.storage.session.state.has('PENDING_DIFFS'));
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'flush-diffs'));
});

test('fields the human left exactly as filled are not corrections', async () => {
	const env = await fresh();
	env.sandbox.document.body.innerHTML = '<form id="f"><input type="text" name="email"></form>';

	env.grab('FormInventory').Extract();
	env.grab('FillHandler').Apply('f1', 'ai@example.com');

	submit(env);
	await env.settle();

	assert.ok(!env.chrome.storage.session.state.has('PENDING_DIFFS'));
});
