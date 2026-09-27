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

function fresh(behavior = null) {
	const env = createEnv({ dom: true, url: 'chrome-extension://panel/index.html' });
	env.sandbox.document.body.innerHTML = panelBodyHtml();

	env.chrome.runtime.behavior = behavior ?? (message => {
		if (message.title === 'lesson-extract') return { reply: 'ok', candidates: [], dropped: [] };
		if (message.title === 'tip') return { id: 2 };
		return { ok: true };
	});

	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/form-inventory.js');
	env.load('controllers/lesson-loop.js');
	env.load('application/dropdown.js');
	env.load('application/compose-ui.js');
	env.load('application/chat-ui.js');
	return env;
}

const $ = (env, id) => env.sandbox.document.getElementById(id);

async function settle(env) {
	for (let i = 0; i < 12; i++) await env.flush();
}

async function initChat(env, mode = 'apply_form', domain = 'ats.example') {
	await env.grab('ChatUi').Init({ mode: () => mode, domain: () => domain });
	await settle(env);
}

function keydown(env, element, options) {
	element.dispatchEvent(new env.sandbox.window.KeyboardEvent('keydown', options));
}

function cardButton(env, label) {
	return Array.from($(env, 'ChatEntries').querySelectorAll('button'))
		.find(b => b.textContent === label);
}

test('the transcript renders user, assistant and report entries from session storage', async () => {
	const env = fresh();
	env.chrome.storage.session.state.set('CHAT_TRANSCRIPT', [
		{ kind: 'user', text: 'only senior roles' },
		{ kind: 'assistant', text: 'Noted.' },
		{ kind: 'report', text: 'saved Ranking lesson' },
	]);
	await initChat(env);

	const entries = Array.from($(env, 'ChatEntries').children);
	assert.deepStrictEqual(entries.map(e => e.textContent), ['only senior roles', 'Noted.', 'saved Ranking lesson']);
	assert.ok(entries[0].classList.contains('fw-semibold'));
	assert.ok(entries[2].classList.contains('muted'));
});

test('a send round-trip appends the user message, reply, dropped notes and candidate cards', async () => {
	const env = fresh(message => {
		if (message.title === 'lesson-extract') return {
			reply: 'Noted.',
			candidates: [{ scope: 'Apply', fieldKey: 'email', value: 'x@y.z', domain: 'ats.example' }],
			dropped: [{ scope: 'Ranking', fieldKey: 'visa_sponsorship', value: 'required', domain: '*', reason: 'scope not allowed in apply_form mode' }],
		};
		return { ok: true };
	});
	await initChat(env);

	$(env, 'ChatInput').value = 'only senior roles, and my email is x@y.z';
	keydown(env, $(env, 'ChatInput'), { key: 'Enter' });
	await settle(env);

	const extract = env.chrome.runtime.sent.find(m => m.title === 'lesson-extract');
	assert.strictEqual(extract.params.tabId, 1);
	assert.strictEqual(extract.params.mode, 'apply_form');
	assert.strictEqual(extract.params.domain, 'ats.example');
	assert.ok(extract.params.text.includes('senior roles'));

	const chat = $(env, 'ChatEntries').textContent;
	assert.ok(chat.includes('only senior roles'));
	assert.ok(chat.includes('Noted.'));
	assert.ok(chat.includes('dropped candidate (Ranking/visa_sponsorship)'));
	assert.ok(chat.includes('proposed lesson'));

	const stored = env.chrome.storage.session.state.get('CHAT_TRANSCRIPT');
	assert.strictEqual(stored.length, 4);
	assert.strictEqual(stored[0].kind, 'user');
	assert.strictEqual(stored[3].kind, 'card');
});

test('Shift+Enter inserts a newline instead of sending', async () => {
	const env = fresh();
	await initChat(env);

	$(env, 'ChatInput').value = 'line one';
	keydown(env, $(env, 'ChatInput'), { key: 'Enter', shiftKey: true });
	await settle(env);

	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'lesson-extract'));
	assert.strictEqual($(env, 'ChatEntries').children.length, 0);
});

test('accepting a ranking card posts the tip route and resolves the card', async () => {
	const env = fresh();
	env.chrome.storage.session.state.set('CHAT_TRANSCRIPT', [
		{ kind: 'card', card: { scope: 'Ranking', fieldKey: 'visa_sponsorship', value: 'required', domain: '*' } },
	]);
	await initChat(env, 'job_detail');

	assert.ok($(env, 'ChatEntries').textContent.includes('proposed lesson'));

	cardButton(env, 'Accept').click();
	await settle(env);

	const tip = env.chrome.runtime.sent.find(m => m.title === 'tip');
	assert.deepStrictEqual(jsonOf(tip.params), {
		scope: 'Ranking',
		domain: '*',
		fieldKey: 'visa_sponsorship',
		value: 'required',
	});

	const stored = env.chrome.storage.session.state.get('CHAT_TRANSCRIPT');
	assert.strictEqual(stored[0].card.resolved, 'accepted');
	assert.ok($(env, 'ChatEntries').textContent.includes('accepted lesson'));
	assert.ok($(env, 'ChatEntries').textContent.includes('saved Ranking lesson'));
});

test('an apply card can be edited inline before accepting', async () => {
	const env = fresh();
	env.chrome.storage.session.state.set('CHAT_TRANSCRIPT', [
		{ kind: 'card', card: { scope: 'Apply', fieldKey: 'email ', value: 'old@example.com', domain: 'ats.example' } },
	]);
	await initChat(env);

	const inputs = Array.from($(env, 'ChatEntries').querySelectorAll('.chat-card input'));
	inputs[0].value = 'Email';
	inputs[1].value = 'new@example.com';

	cardButton(env, 'Accept').click();
	await settle(env);

	const tip = env.chrome.runtime.sent.find(m => m.title === 'tip');
	assert.strictEqual(tip.params.scope, 'Apply');
	assert.strictEqual(tip.params.domain, 'ats.example');
	assert.strictEqual(tip.params.fieldKey, 'email');
	assert.strictEqual(tip.params.fieldLabel, 'Email');
	assert.strictEqual(tip.params.value, 'new@example.com');
});

test('rejecting a card discards it without any tip post', async () => {
	const env = fresh();
	env.chrome.storage.session.state.set('CHAT_TRANSCRIPT', [
		{ kind: 'card', card: { scope: 'Apply', fieldKey: 'email', value: 'x', domain: 'ats.example' } },
	]);
	await initChat(env);

	cardButton(env, 'Reject').click();
	await settle(env);

	assert.ok(!env.chrome.runtime.sent.some(m => m.title === 'tip'));

	const stored = env.chrome.storage.session.state.get('CHAT_TRANSCRIPT');
	assert.strictEqual(stored[0].card.resolved, 'rejected');
	assert.ok($(env, 'ChatEntries').textContent.includes('discarded'));
});

test('compose drafts rehydrate into the live drafts block at the end of the chat', async () => {
	const draft = {
		id: 'ats.example::cover', domain: 'ats.example', fieldId: 'f2',
		fieldKey: 'cover', fieldLabel: 'Cover letter', text: 'Dear team', accepted: false,
	};
	const env = fresh(message => {
		if (message.title === 'compose-list') return { drafts: [draft] };
		return { ok: true };
	});
	await initChat(env);
	await env.grab('ComposeUI').Init();
	await settle(env);

	assert.ok($(env, 'ChatLogArea').contains($(env, 'ComposeList')));
	assert.strictEqual($(env, 'ComposeList').querySelector('textarea').value, 'Dear team');
	assert.ok($(env, 'ComposeList').textContent.includes('pending review'));
});

test('the input is disabled while the assistant call is in flight', async () => {
	const env = fresh();
	await initChat(env);

	let release = null;
	env.chrome.runtime.sendMessage = (message, callback) => {
		release = () => callback({ reply: 'ok', candidates: [] });
	};

	$(env, 'ChatInput').value = 'remember this';
	$(env, 'ChatSend').click();
	await settle(env);

	assert.strictEqual(typeof release, 'function');
	assert.strictEqual($(env, 'ChatInput').disabled, true);
	assert.strictEqual($(env, 'ChatSend').disabled, true);

	release();
	await settle(env);

	assert.strictEqual($(env, 'ChatInput').disabled, false);
	assert.ok($(env, 'ChatEntries').textContent.includes('remember this'));
	assert.ok($(env, 'ChatEntries').textContent.includes('ok'));
});

test('an llm-busy reply surfaces as a chat error entry', async () => {
	const env = fresh(message => {
		if (message.title === 'lesson-extract') return { error: 'llm-busy' };
		return { ok: true };
	});
	await initChat(env);

	$(env, 'ChatInput').value = 'hello';
	$(env, 'ChatSend').click();
	await settle(env);

	const entries = Array.from($(env, 'ChatEntries').children);
	assert.ok(entries.some(e => e.classList.contains('warn') && e.textContent.includes('llm-busy')));
});

test('the transcript is capped at 100 entries', async () => {
	const env = fresh();
	const many = Array.from({ length: 120 }, (_, i) => ({ kind: 'report', text: 'r' + i }));
	env.chrome.storage.session.state.set('CHAT_TRANSCRIPT', many);
	await initChat(env);

	await env.grab('ChatUi').Report('overflow');
	await settle(env);

	const stored = env.chrome.storage.session.state.get('CHAT_TRANSCRIPT');
	assert.strictEqual(stored.length, 100);
	assert.strictEqual(stored.at(-1).text, 'overflow');
	assert.strictEqual($(env, 'ChatEntries').children.length, 100);
});
