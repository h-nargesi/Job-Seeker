import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createEnv, EXTENSION_ROOT, jsonOf } from './helpers/env.js';
import { createChrome } from './helpers/chrome-mock.js';

const manifest = JSON.parse(fs.readFileSync(path.join(EXTENSION_ROOT, 'manifest.json'), 'utf8'));

function panelBodyHtml() {
	const html = fs.readFileSync(path.join(EXTENSION_ROOT, 'application/panel.html'), 'utf8');
	const body = html.match(/<body>([\s\S]*)<\/body>/)[1];
	return body.replace(/<script[\s\S]*?<\/script>/g, '');
}

function fresh(job = null, memory = [], behavior = null, chrome) {
	const env = createEnv({ dom: true, url: 'chrome-extension://panel/index.html', chrome });
	env.sandbox.document.body.innerHTML = panelBodyHtml();

	env.chrome.runtime.behavior = behavior ?? (message => {
		if (message.title === 'job') return job ?? { error: 'http', status: 404 };
		if (message.title === 'memory-list') return memory;
		if (message.title === 'tip') return { id: 2 };
		if (message.title === 'lesson-extract') return { reply: 'ok', candidates: [], dropped: [] };
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

function pressEnter(env, element) {
	const event = new env.sandbox.window.KeyboardEvent('keyup');
	Object.defineProperty(event, 'keyCode', { value: 13 });
	element.dispatchEvent(event);
}

test('Load renders the job line and warns on a pending proposal', async () => {
	const env = fresh({ jobId: 5, title: 'Senior .NET', url: 'https://ats.example/5', aiScore: 82, pendingProposal: true, resumeText: 'Ryan' }, []);
	await settle(env);

	$(env, 'JobId').value = '5';
	$(env, 'LoadJob').click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'job' && m.params.jobId === 5));
	assert.ok($(env, 'JobInfo').textContent.includes('#5 Senior .NET (score 82)'));
	assert.ok($(env, 'JobInfo').textContent.includes('pending resume proposal'));
});

test('a missing job id renders the 404 as a status hint', async () => {
	const env = fresh(null, []);
	await settle(env);

	$(env, 'JobId').value = '77';
	$(env, 'LoadJob').click();
	await settle(env);

	assert.strictEqual($(env, 'JobsStatus').textContent, 'job not found');
	assert.strictEqual($(env, 'JobInfo').textContent, '');
});

test('Enter in the job id field loads; invalid input only hints', async () => {
	const env = fresh({ jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: '' }, []);
	await settle(env);

	$(env, 'JobId').value = 'oops';
	pressEnter(env, $(env, 'JobId'));
	await settle(env);

	assert.strictEqual($(env, 'JobsStatus').textContent, 'enter a job id');
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'job'));

	$(env, 'JobId').value = '/job/get/5';
	pressEnter(env, $(env, 'JobId'));
	await settle(env);

	assert.strictEqual($(env, 'JobsStatus').textContent, 'enter a job id');
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'job'));
});

test('the job id field strips non-digits on input', async () => {
	const env = fresh(null, []);
	await settle(env);

	$(env, 'JobId').value = '12a7b';
	$(env, 'JobId').dispatchEvent(new env.sandbox.window.Event('input'));

	assert.strictEqual($(env, 'JobId').value, '127');
});

test('the panel header shows the manifest version_name', async () => {
	const env = fresh(null, []);
	await settle(env);

	assert.strictEqual($(env, 'Version').textContent, manifest.version_name ?? manifest.version);
});

test('the panel header falls back to version when version_name is absent', async () => {
	const chrome = createChrome();
	chrome.runtime.getManifest = () => ({ ...manifest, version_name: undefined });
	const env = fresh(null, [], null, chrome);
	await settle(env);

	assert.strictEqual($(env, 'Version').textContent, manifest.version);
});

test('the panel is two tabs — Assistant and Memory — with the chat inside Assistant', async () => {
	const env = fresh(null, []);
	await settle(env);

	assert.strictEqual($(env, 'ShowJobs').textContent, 'Assistant');
	assert.strictEqual($(env, 'ShowMemory').textContent, 'Memory');

	assert.ok($(env, 'ShowJobs').classList.contains('active'));
	assert.ok($(env, 'ModeOverride').closest('#JobsView'), 'mode override lives inside the Assistant tab');
	assert.ok($(env, 'ChatInput'));
	assert.ok($(env, 'ChatSend'));
	assert.ok($(env, 'ChatLogArea').contains($(env, 'ComposeList')), 'drafts render at the end of the chat stream');

	assert.ok(!$(env, 'ComposeView'));
	assert.ok(!$(env, 'ComposeStatus'));
	assert.ok($(env, 'SaveTip').closest('#MemoryView'), 'the manual add-lesson form stays in the Memory tab');
	for (const id of ['TipScope', 'TipRankingKey', 'TipFieldKey', 'TipValue', 'TipNote', 'ChatLog'])
		assert.ok($(env, id), `${id} should exist in the Memory tab`);

	$(env, 'ShowMemory').click();
	await settle(env);

	assert.ok($(env, 'ShowMemory').classList.contains('active'));
	assert.ok(!$(env, 'ShowJobs').classList.contains('active'));
	assert.strictEqual($(env, 'MemoryView').style.display, '');
	assert.strictEqual($(env, 'JobsView').style.display, 'none');

	$(env, 'ShowJobs').click();
	await settle(env);

	assert.strictEqual($(env, 'JobsView').style.display, '');
});

test('the panel holds no settings inputs — settings live only in the popup', async () => {
	const env = fresh(null, []);
	await settle(env);

	for (const id of ['ServerUrl', 'ApiKey', 'LlamaUrl', 'LlamaModel'])
		assert.ok(!$(env, id), `${id} should not exist in the panel`);
});

test('the panel follows theme changes made in the popup', async () => {
	const env = fresh(null, []);
	await settle(env);

	assert.strictEqual(env.sandbox.document.documentElement.getAttribute('data-bs-theme'), 'light');

	await env.chrome.storage.local.set({ THEME: 'dark' });
	await settle(env);

	assert.strictEqual(env.sandbox.document.documentElement.getAttribute('data-bs-theme'), 'dark');
});

test('mode override switches the badge and persists to session storage', async () => {
	const env = fresh(null, []);
	await settle(env);

	const override = $(env, 'ModeOverride');
	override.value = 'job_detail';
	override.dispatchEvent(new env.sandbox.window.Event('change'));
	await settle(env);

	assert.ok($(env, 'Mode').textContent.includes('job_detail'));
	assert.strictEqual(env.chrome.storage.session.state.get('MODE_OVERRIDE'), 'job_detail');
});

test('Applied is a human click that posts the entered id with no prior load', async () => {
	const env = fresh(null, []);
	await settle(env);

	$(env, 'JobId').value = '7';
	$(env, 'MarkApplied').click();
	await settle(env);

	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'job'));
	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'applied' && m.params.jobId === 7));
	assert.ok($(env, 'JobsStatus').textContent.includes('marked applied #7'));
});

test('Applied error result surfaces in the status line', async () => {
	const env = fresh(null, [], message => {
		if (message.title === 'applied') return { error: 'http', status: 404 };
		return { ok: true };
	});
	await settle(env);

	$(env, 'JobId').value = '7';
	$(env, 'MarkApplied').click();
	await settle(env);

	assert.ok($(env, 'JobsStatus').textContent.includes('applied error'));
});

test('Fill guards on the loaded job, then targets the active tab', async () => {
	const job = { jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: 'RYAN-RESUME' };
	const env = fresh(job, [], message => {
		if (message.title === 'job') return job;
		if (message.title === 'fill') return { done: true, filled: 3, writes: 1 };
		return { ok: true };
	});
	await settle(env);

	$(env, 'FillJob').click();
	await settle(env);

	assert.strictEqual($(env, 'JobsStatus').textContent, 'load the job first');
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'fill'));

	$(env, 'JobId').value = '5';
	$(env, 'LoadJob').click();
	await settle(env);

	$(env, 'FillJob').click();
	await settle(env);

	const fill = env.chrome.runtime.sent.find(m => m.title === 'fill');
	assert.deepStrictEqual(jsonOf(fill.params), { tabId: 1, jobId: 5, resumeText: 'RYAN-RESUME' });
	assert.ok($(env, 'JobsStatus').textContent.includes('filled 3'));
	assert.ok($(env, 'JobsStatus').textContent.includes('submit yourself'));
});

test('Fill posts the model summary with its not-filled list into the chat', async () => {
	const job = { jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: 'RYAN-RESUME' };
	const env = fresh(job, [], message => {
		if (message.title === 'job') return job;
		if (message.title === 'fill') return {
			done: true,
			filled: 2,
			writes: 0,
			content: 'Filled 2 fields.\nNot filled:\nEmail — not in resume or memory',
		};
		return { ok: true };
	});
	await settle(env);

	$(env, 'JobId').value = '5';
	$(env, 'LoadJob').click();
	await settle(env);

	$(env, 'FillJob').click();
	await settle(env);

	const chat = $(env, 'ChatEntries').textContent;
	assert.ok(chat.includes('Not filled:'));
	assert.ok(chat.includes('Email — not in resume or memory'));
});

test('Open and Compose guard on the loaded job too', async () => {
	const env = fresh(null, []);
	await settle(env);

	$(env, 'OpenJob').click();
	$(env, 'ComposeJob').click();
	await settle(env);

	assert.strictEqual($(env, 'JobsStatus').textContent, 'load the job first');
	assert.strictEqual(env.chrome.tabs.created.length, 0);
	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'compose'));
});

test('memory rows render with confirm, edit and delete actions', async () => {
	const rows = [{
		memoryID: 9, scope: 'Apply', kind: 'Correction', confirmed: false,
		fieldKey: 'email', value: 'x@example.com', agencyDomain: '*', useCount: 0,
	}];
	const env = fresh(null, rows);
	await settle(env);

	$(env, 'ShowMemory').click();
	await settle(env);

	const labels = Array.from($(env, 'MemoryList').querySelectorAll('button')).map(b => b.textContent);
	assert.deepStrictEqual(labels, ['Confirm', 'Edit', 'Delete']);
	assert.ok($(env, 'MemoryList').textContent.includes('[Apply/Correction pending]'));
});

test('confirm toggles post memory-confirm and refresh the list', async () => {
	const rows = [{ memoryID: 9, scope: 'Apply', kind: 'Tip', confirmed: false, fieldKey: 'k', value: 'v', agencyDomain: '*', useCount: 0 }];
	const env = fresh(null, rows);
	await settle(env);

	$(env, 'ShowMemory').click();
	await settle(env);

	Array.from($(env, 'MemoryList').querySelectorAll('button')).find(b => b.textContent === 'Confirm').click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'memory-confirm' && m.params.id === 9 && m.params.confirmed === true));
});

test('the memory list stays lazy until the memory view is opened', async () => {
	const env = fresh(null, []);
	await settle(env);

	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'memory-list'));

	$(env, 'ShowMemory').click();
	await settle(env);

	assert.ok(env.chrome.runtime.sent.some(m => m.title === 'memory-list'));
});

test('the memorycap warning appears beyond 500 confirmed rows', async () => {
	const env = fresh(null, []);
	await settle(env);

	const rows = Array.from({ length: 501 }, (_, i) => ({
		memoryID: i, scope: 'Apply', kind: 'Tip', confirmed: true, fieldKey: 'k' + i, value: 'v', agencyDomain: '*', useCount: 0,
	}));
	env.grab('RenderMemory')(rows);

	assert.ok($(env, 'MemoryCap').textContent.includes('memorycap'));
	assert.ok($(env, 'MemoryCap').textContent.includes('501'));
});

test('apply_form pages only accept apply-scope lessons', async () => {
	const env = fresh(null, []);
	await settle(env);

	assert.deepStrictEqual(Array.from($(env, 'TipScope').options).map(o => o.value), ['Apply']);
});

test('job_detail mode offers ranking and delta lessons with the closed key list', async () => {
	const env = fresh(null, []);
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

test('Compose sends tab id and resume text, then renders pending drafts in the chat stream', async () => {
	const job = { jobId: 5, title: 'Dev', url: 'u', pendingProposal: false, resumeText: 'RYAN-RESUME' };
	const draft = {
		id: 'ats.example::cover', domain: 'ats.example', fieldId: 'f2',
		fieldKey: 'cover', fieldLabel: 'Cover letter', text: 'Dear team', accepted: false,
	};
	const env = fresh(job, [], message => {
		if (message.title === 'job') return job;
		if (message.title === 'compose') return { drafts: [draft] };
		if (message.title === 'compose-list') return { drafts: [draft] };
		return { ok: true };
	});
	await settle(env);

	$(env, 'JobId').value = '5';
	$(env, 'LoadJob').click();
	await settle(env);

	$(env, 'ComposeJob').click();
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

	assert.ok($(env, 'ChatLogArea').contains($(env, 'ComposeList')));
	assert.ok($(env, 'ChatEntries').textContent.includes('drafts are pending'));
});

test('accepting a draft posts the edited text and reports into the chat', async () => {
	let current = {
		id: 'ats.example::cover', domain: 'ats.example', fieldId: 'f2',
		fieldKey: 'cover', fieldLabel: 'Cover letter', text: 'Dear team', accepted: false,
	};
	const env = fresh(null, [], message => {
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
	assert.ok($(env, 'ChatEntries').textContent.includes('accepted — press Fill current tab'));
	assert.strictEqual($(env, 'ComposeList').querySelector('textarea').readOnly, true);
});

test('no panel control submits the form — the chat Send is not a form submit', async () => {
	const env = fresh(null, []);
	await settle(env);

	const buttons = Array.from(env.sandbox.document.querySelectorAll('button'));
	assert.ok(buttons.length >= 5);
	for (const button of buttons) {
		if (button.id === 'ChatSend') continue;
		assert.ok(!/submit|send\b/i.test(button.textContent), button.textContent);
	}
});
