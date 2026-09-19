import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createEnv, EXTENSION_ROOT, jsonOf } from './helpers/env.js';

function panelBodyHtml() {
	const html = fs.readFileSync(path.join(EXTENSION_ROOT, 'application/panel.html'), 'utf8');
	const body = html.match(/<body>([\s\S]*)<\/body>/)[1];
	return body.replace(/<script[\s\S]*?<\/script>/g, '');
}

function fresh(jobs = [], memory = []) {
	const env = createEnv({ dom: true, url: 'chrome-extension://panel/index.html' });
	env.sandbox.document.body.innerHTML = panelBodyHtml();

	env.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081/');
	env.chrome.storage.local.state.set('API_KEY', 'assistant-key');
	env.chrome.runtime.behavior = message => {
		if (message.title === 'jobs') return jobs;
		if (message.title === 'memory-list') return memory;
		if (message.title === 'tip') return { id: 2 };
		return { ok: true };
	};

	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/form-inventory.js');
	env.load('application/panel.js');
	return env;
}

const $ = (env, id) => env.sandbox.document.getElementById(id);

async function settle(env) {
	for (let i = 0; i < 12; i++) await env.flush();
}

test('loads settings, renders attention jobs and warns on pending proposals', async () => {
	const env = fresh(
		[{ jobId: 5, title: 'Senior .NET', url: 'https://ats.example/5', aiScore: 82, pendingProposal: true, resumeText: 'Ryan' }],
		[],
	);
	await settle(env);

	assert.strictEqual($(env, 'ServerUrl').value, 'https://core.example:8081/');
	assert.strictEqual($(env, 'ApiKey').value, 'assistant-key');
	assert.strictEqual($(env, 'JobsStatus').textContent, '1 attention job(s)');
	assert.ok($(env, 'JobsList').textContent.includes('Senior .NET'));
	assert.ok($(env, 'JobsList').textContent.includes('pending resume proposal'));
});

test('Applied is a human click that posts the job id', async () => {
	const env = fresh([{ jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: '' }], []);
	await settle(env);

	const button = Array.from($(env, 'JobsList').querySelectorAll('button')).find(b => b.textContent === 'Applied');
	button.click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'applied' && m.params.jobId === 5));
	assert.ok($(env, 'JobsStatus').textContent.includes('marked applied'));
});

test('Fill current tab sends tab id, job id and resume text', async () => {
	const env = fresh([{ jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: 'RYAN-RESUME' }], []);
	env.chrome.runtime.behavior = message => {
		if (message.title === 'jobs') return [{ jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: 'RYAN-RESUME' }];
		if (message.title === 'fill') return { done: true, filled: 3, writes: 1 };
	 return { ok: true };
	};
	await settle(env);

	const button = Array.from($(env, 'JobsList').querySelectorAll('button')).find(b => b.textContent === 'Fill current tab');
	button.click();
	await settle(env);

	const fill = env.chrome.runtime.sent.find(m => m.title === 'fill');
	assert.deepStrictEqual(jsonOf(fill.params), { tabId: 1, jobId: 5, resumeText: 'RYAN-RESUME' });
	assert.ok($(env, 'JobsStatus').textContent.includes('filled 3'));
	assert.ok($(env, 'JobsStatus').textContent.includes('submit yourself'));
});

test('memory rows render with confirm, edit and delete actions', async () => {
	const rows = [{
		memoryID: 9, scope: 'Apply', kind: 'Correction', confirmed: false,
		fieldKey: 'email', value: 'x@example.com', agencyDomain: '*', useCount: 0,
	}];
	const env = fresh([], rows);
	await settle(env);

	const labels = Array.from($(env, 'MemoryList').querySelectorAll('button')).map(b => b.textContent);
	assert.deepStrictEqual(labels, ['Confirm', 'Edit', 'Delete']);
	assert.ok($(env, 'MemoryList').textContent.includes('[Apply/Correction pending]'));
});

test('confirm toggles post memory-confirm and refresh the list', async () => {
	const rows = [{ memoryID: 9, scope: 'Apply', kind: 'Tip', confirmed: false, fieldKey: 'k', value: 'v', agencyDomain: '*', useCount: 0 }];
	const env = fresh([], rows);
	await settle(env);

	Array.from($(env, 'MemoryList').querySelectorAll('button')).find(b => b.textContent === 'Confirm').click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'memory-confirm' && m.params.id === 9 && m.params.confirmed === true));
});

test('the memorycap warning appears beyond 500 confirmed rows', async () => {
	const env = fresh([], []);
	await settle(env);

	const rows = Array.from({ length: 501 }, (_, i) => ({
		memoryID: i, scope: 'Apply', kind: 'Tip', confirmed: true, fieldKey: 'k' + i, value: 'v', agencyDomain: '*', useCount: 0,
	}));
	env.grab('RenderMemory')(rows);

	assert.ok($(env, 'MemoryCap').textContent.includes('memorycap'));
	assert.ok($(env, 'MemoryCap').textContent.includes('501'));
});

test('apply_form pages only accept apply-scope lessons', async () => {
	const env = fresh([], []);
	await settle(env);

	assert.deepStrictEqual(Array.from($(env, 'TipScope').options).map(o => o.value), ['Apply']);
});

test('job_detail mode offers ranking and delta lessons with the closed key list', async () => {
	const env = fresh([], []);
	await settle(env);

	const override = $(env, 'ModeOverride');
	override.value = 'job_detail';
	override.dispatchEvent(new env.sandbox.window.Event('change'));
	await settle(env);

	assert.deepStrictEqual(Array.from($(env, 'TipScope').options).map(o => o.value), ['Ranking', 'Resume']);
	assert.ok($(env, 'Mode').textContent.includes('job_detail'));

	$(env, 'TipScope').value = 'Ranking';
	env.grab('RenderTipFields')();
	assert.strictEqual($(env, 'TipRankingKey').style.display, '');
	assert.ok(Array.from($(env, 'TipRankingKey').options).length >= 8);

	$(env, 'TipValue').value = 'required';
	$(env, 'SaveTip').click();
	await settle(env);

	const tip = env.chrome.runtime.sent.find(m => m.title === 'tip');
	assert.strictEqual(tip.params.scope, 'Ranking');
	assert.strictEqual(tip.params.fieldKey, 'visa_sponsorship');
	assert.strictEqual(tip.params.domain, '*');
	assert.strictEqual(tip.params.value, 'required');

	assert.ok($(env, 'ChatLog').textContent.includes('saved Ranking lesson'));
});
