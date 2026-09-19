import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, deliver, jsonOf } from './helpers/env.js';

async function fresh(url) {
	const env = createEnv({ dom: true, url });
	env.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081/');
	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/form-inventory.js');
	env.load('controllers/fill-handler.js');
	env.load('controllers/submit-diff.js');
	env.load('controllers/assistant-page.js');
	await env.settle();
	return env;
}

test('a core origin defaults the mode to job_detail', async () => {
	const env = await fresh('https://core.example:8081/job/get/123');
	const state = await deliver(env.chrome, { title: 'page-state' });

	assert.strictEqual(state.mode, 'job_detail');
	assert.strictEqual(state.jobId, 123);
	assert.strictEqual(state.domain, 'core.example');
});

test('any other origin defaults the mode to apply_form', async () => {
	const env = await fresh('https://ats.example/apply?jobid=45');
	const state = await deliver(env.chrome, { title: 'page-state' });

	assert.strictEqual(state.mode, 'apply_form');
	assert.strictEqual(state.jobId, 45);
});

test('job id comes from ?jobid= or /job/get/{id} and is null otherwise', async () => {
	const env = await fresh('https://ats.example/apply');
	assert.strictEqual((await deliver(env.chrome, { title: 'page-state' })).jobId, null);

	const env2 = await fresh('https://core.example:8081/job/get/77');
	assert.strictEqual((await deliver(env2.chrome, { title: 'page-state' })).jobId, 77);
});

test('the inventory message returns controls plus page mode without raw html', async () => {
	const env = await fresh('https://ats.example/apply?jobid=8');
	env.sandbox.document.body.innerHTML = `
		<label for="e">Email</label>
		<input id="e" type="text" name="email" />
	`;

	const state = await deliver(env.chrome, { title: 'inventory' });

	assert.strictEqual(state.domain, 'ats.example');
	assert.strictEqual(state.mode, 'apply_form');
	assert.strictEqual(state.jobId, 8);
	assert.strictEqual(state.inventory.length, 1);
	assert.strictEqual(state.inventory[0].fieldKey, 'email');
	assert.ok(!JSON.stringify(state).includes('querySelector'));
});

test('the apply-fill message fills through the registered inventory', async () => {
	const env = await fresh('https://ats.example/apply');
	env.sandbox.document.body.innerHTML = '<input type="text" name="email">';

	await deliver(env.chrome, { title: 'inventory' });
	const result = await deliver(env.chrome, {
		title: 'apply-fill',
		params: { fieldId: 'f1', value: 'ryan@example.com' },
	});

	assert.deepStrictEqual(jsonOf(result), { ok: true });
	assert.strictEqual(env.sandbox.document.querySelector('input').value, 'ryan@example.com');
});

test('unknown titles answer with an error object', async () => {
	const env = await fresh('https://ats.example/apply');
	const result = await deliver(env.chrome, { title: 'nope' });

	assert.deepStrictEqual(jsonOf(result), { error: 'unknown-title' });
});

test('the page never posts anywhere on its own — no outbound messages without a request', async () => {
	const env = await fresh('https://ats.example/apply');
	await env.settle();

	assert.strictEqual(env.chrome.runtime.sent.length, 0);
	assert.strictEqual(env.fetchStub.calls.length, 0);
});
