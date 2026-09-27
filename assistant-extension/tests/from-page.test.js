import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createEnv, EXTENSION_ROOT } from './helpers/env.js';

function panelBodyHtml() {
	const html = fs.readFileSync(path.join(EXTENSION_ROOT, 'application/panel.html'), 'utf8');
	const body = html.match(/<body>([\s\S]*)<\/body>/)[1];
	return body.replace(/<script[\s\S]*?<\/script>/g, '');
}

function fresh(job = null, behavior = null) {
	const env = createEnv({ dom: true, url: 'chrome-extension://panel/index.html' });
	env.sandbox.document.body.innerHTML = panelBodyHtml();

	env.chrome.runtime.behavior = behavior ?? (message => {
		if (message.title === 'job') return job ?? { error: 'http', status: 404 };
		if (message.title === 'memory-list') return [];
		if (message.title === 'tip') return { id: 2 };
		return { ok: true };
	});

	env.load('controllers/storage-handler.js');
	env.load('application/theme.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/form-inventory.js');
	env.load('controllers/lesson-loop.js');
	env.load('application/dropdown.js');
	env.load('application/compose-ui.js');
	env.load('application/chat-ui.js');
	env.load('application/panel.js');
	return env;
}

const $ = (env, id) => env.sandbox.document.getElementById(id);

async function settle(env) {
	for (let i = 0; i < 12; i++) await env.flush();
}

test('FromPage reads the job id from the dashboard job-details URL', async () => {
	const env = fresh({ jobId: 123, title: 'Dev', url: 'u', pendingProposal: false, resumeText: '' });
	env.chrome.tabs.activeUrl = 'http://localhost:8081/job/get/123';
	await settle(env);

	$(env, 'FromPage').click();
	await settle(env);

	assert.strictEqual($(env, 'JobId').value, '123');
	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'job' && m.params.jobId === 123));
	assert.ok($(env, 'JobInfo').textContent.includes('#123'));
});

test('FromPage ignores query and hash and works on non-localhost origins', async () => {
	const env = fresh({ jobId: 77, title: 'Dev', url: 'u', pendingProposal: false, resumeText: '' });
	env.chrome.tabs.activeUrl = 'http://192.168.1.20:8081/job/get/77?print=1#top';
	await settle(env);

	$(env, 'FromPage').click();
	await settle(env);

	assert.strictEqual($(env, 'JobId').value, '77');
	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'job' && m.params.jobId === 77));
});

test('FromPage accepts a trailing slash on the job path', async () => {
	const env = fresh({ jobId: 9, title: 'Dev', url: 'u', pendingProposal: false, resumeText: '' });
	env.chrome.tabs.activeUrl = 'http://localhost:8081/job/get/9/';
	await settle(env);

	$(env, 'FromPage').click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'job' && m.params.jobId === 9));
});

test('FromPage does not false-positive on agency pages or non-numeric ids', async () => {
	const env = fresh(null);
	await settle(env);

	env.chrome.tabs.activeUrl = 'https://ats.example/jobs/view/999?next=/job/get/9';
	$(env, 'FromPage').click();
	await settle(env);

	assert.ok($(env, 'JobsStatus').textContent.includes('not a job details page'));
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'job'));

	env.chrome.tabs.activeUrl = 'http://localhost:8081/job/get/abc';
	$(env, 'FromPage').click();
	await settle(env);

	assert.ok($(env, 'JobsStatus').textContent.includes('not a job details page'));
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'job'));
});

test('FromPage on a non-dashboard page only hints', async () => {
	const env = fresh(null);
	await settle(env);

	$(env, 'FromPage').click();
	await settle(env);

	assert.ok($(env, 'JobsStatus').textContent.includes('not a job details page'));
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'job'));
});

test('FromPage distinguishes an unreadable tab url', async () => {
	const env = fresh(null);
	env.chrome.tabs.activeUrl = undefined;
	await settle(env);

	$(env, 'FromPage').click();
	await settle(env);

	assert.ok($(env, 'JobsStatus').textContent.includes('cannot read the current tab url'));
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'job'));
});
