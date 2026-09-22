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

function fresh(jobs = [], memory = [], behavior = null) {
	const env = createEnv({ dom: true, url: 'chrome-extension://panel/index.html' });
	env.sandbox.document.body.innerHTML = panelBodyHtml();

	env.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081/');
	env.chrome.storage.local.state.set('API_KEY', 'assistant-key');
	env.chrome.runtime.behavior = behavior ?? (message => {
		if (message.title === 'jobs') return jobs;
		if (message.title === 'memory-list') return memory;
		if (message.title === 'tip') return { id: 2 };
		return { ok: true };
	});

	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/form-inventory.js');
	env.load('application/compose-ui.js');
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

	$(env, 'ShowMemory').click();
	await settle(env);

	const labels = Array.from($(env, 'MemoryList').querySelectorAll('button')).map(b => b.textContent);
	assert.deepStrictEqual(labels, ['Confirm', 'Edit', 'Delete']);
	assert.ok($(env, 'MemoryList').textContent.includes('[Apply/Correction pending]'));
});

test('confirm toggles post memory-confirm and refresh the list', async () => {
	const rows = [{ memoryID: 9, scope: 'Apply', kind: 'Tip', confirmed: false, fieldKey: 'k', value: 'v', agencyDomain: '*', useCount: 0 }];
	const env = fresh([], rows);
	await settle(env);

	$(env, 'ShowMemory').click();
	await settle(env);

	Array.from($(env, 'MemoryList').querySelectorAll('button')).find(b => b.textContent === 'Confirm').click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'memory-confirm' && m.params.id === 9 && m.params.confirmed === true));
});

test('the memory list stays lazy until the memory view is opened', async () => {
	const env = fresh([], []);
	await settle(env);

	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'memory-list'));

	$(env, 'ShowMemory').click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'memory-list'));
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

test('Compose sends tab id and resume text, then renders pending drafts for review', async () => {
	const job = { jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: 'RYAN-RESUME' };
	const draft = {
		id: 'ats.example::cover', domain: 'ats.example', fieldId: 'f2',
		fieldKey: 'cover', fieldLabel: 'Cover letter', text: 'Dear team', accepted: false,
	};
	const env = fresh([job], []);
	env.chrome.runtime.behavior = message => {
		if (message.title === 'jobs') return [job];
		if (message.title === 'compose') return { drafts: [draft] };
		if (message.title === 'compose-list') return { drafts: [draft] };
		return { ok: true };
	};
	await settle(env);

	const button = Array.from($(env, 'JobsList').querySelectorAll('button')).find(b => b.textContent === 'Compose');
	button.click();
	await settle(env);

	const compose = env.chrome.runtime.sent.find(m => m.title === 'compose');
	assert.strictEqual(compose.params.tabId, 1);
	assert.strictEqual(compose.params.jobId, 5);
	assert.strictEqual(compose.params.resumeText, 'RYAN-RESUME');

	assert.ok($(env, 'ComposeList').textContent.includes('Cover letter'));
	assert.ok($(env, 'ComposeList').textContent.includes('pending review'));
	const area = $(env, 'ComposeList').querySelector('textarea');
	assert.strictEqual(area.value, 'Dear team');
	assert.strictEqual(area.readOnly, false);
	const labels = Array.from($(env, 'ComposeList').querySelectorAll('button')).map(b => b.textContent);
	assert.deepStrictEqual(labels, ['Accept', 'Reject']);
});

test('accepting a draft posts the edited text and shows the accepted state', async () => {
	let current = {
		id: 'ats.example::cover', domain: 'ats.example', fieldId: 'f2',
		fieldKey: 'cover', fieldLabel: 'Cover letter', text: 'Dear team', accepted: false,
	};
	const env = fresh([], [], message => {
		if (message.title === 'compose-list') return { drafts: [current] };
		if (message.title === 'compose-accept') {
			current = { ...current, accepted: true, text: message.params.text };
			return { ok: true };
		}
		return { ok: true };
	});
	await settle(env);

	const area = $(env, 'ComposeList').querySelector('textarea');
	area.value = 'Edited draft';
	Array.from($(env, 'ComposeList').querySelectorAll('button')).find(b => b.textContent === 'Accept').click();
	await settle(env);

	const accept = env.chrome.runtime.sent.find(m => m.title === 'compose-accept');
	assert.strictEqual(accept.params.id, 'ats.example::cover');
	assert.strictEqual(accept.params.text, 'Edited draft');

	assert.ok($(env, 'ComposeList').textContent.includes('accepted — Fill current tab applies it'));
	assert.ok($(env, 'ComposeStatus').textContent.includes('Fill current tab'));
	assert.strictEqual($(env, 'ComposeList').querySelector('textarea').readOnly, true);
});

test('no panel control submits the form', async () => {
	const env = fresh([], []);
	await settle(env);

	const labels = Array.from(env.sandbox.document.querySelectorAll('button')).map(b => b.textContent);
	assert.ok(labels.length >= 5);
	for (const label of labels) assert.ok(!/submit|send\b/i.test(label), label);
});
