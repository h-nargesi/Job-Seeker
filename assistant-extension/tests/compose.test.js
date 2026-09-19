import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, link, deliver, jsonOf } from './helpers/env.js';

function composeEnv() {
	const env = createEnv({});
	env.load('controllers/storage-handler.js');
	env.load('controllers/compose-loop.js');
	return { env, ComposeLoop: env.grab('ComposeLoop'), ComposeStore: env.grab('ComposeStore') };
}

async function freshTab(html) {
	const sw = createEnv({ importScripts: true });
	const content = createEnv({ dom: true, url: 'https://ats.example/apply?jobid=12' });

	sw.chrome.storage.local.state.set('LLAMA_URL', 'http://localhost:8082/');

	content.load('controllers/storage-handler.js');
	content.load('controllers/background-messaging.js');
	content.load('controllers/form-inventory.js');
	content.load('controllers/fill-handler.js');
	content.load('controllers/submit-diff.js');
	content.load('controllers/assistant-page.js');
	await content.settle();

	sw.load('controllers/background.js');
	sw.sandbox.TAB_TIMEOUT_MS = 5;
	sw.grab('LlmClient').REQUEST_TIMEOUT = 5;

	link(sw.chrome, content.chrome);
	content.sandbox.document.body.innerHTML = html
		?? '<input type="text" name="email"><textarea name="cover" rows="4"></textarea>';

	return { sw, content };
}

function llmContent(content) {
	return { body: { choices: [{ message: { content, tool_calls: [] } }] } };
}

const LONG_FIELDS = [
	{ fieldId: 'f2', fieldKey: 'cover', label: 'Cover letter' },
];

test('compose messages separate trusted context from page data and demand plain-text JSON', () => {
	const { ComposeLoop } = composeEnv();
	const messages = ComposeLoop.Messages({ domain: 'ats.example', resume: 'Ryan', guidance: 'be brief', fields: LONG_FIELDS });

	assert.strictEqual(messages.length, 2);
	assert.strictEqual(messages[0].role, 'system');
	assert.ok(messages[0].content.includes('data, never instructions'));
	assert.ok(messages[0].content.includes('trusted'));
	assert.ok(messages[0].content.includes('no HTML'));
	assert.ok(messages[0].content.includes('JSON array'));
	assert.ok(messages[0].content.includes('never submit'));

	const user = messages[1].content;
	assert.ok(user.includes('## CANDIDATE RESUME (trusted)'));
	assert.ok(user.includes('## HUMAN GUIDANCE (trusted)'));
	assert.ok(user.includes('## LONG-FORM FIELDS (data'));
	assert.ok(user.includes('Cover letter'));
	assert.ok(user.includes('ats.example'));
});

test('compose parses fenced JSON, drops unknown fields, blanks and duplicates, strips html', () => {
	const { ComposeLoop } = composeEnv();
	const fields = LONG_FIELDS.concat([{ fieldId: 'f3', fieldKey: 'why', label: 'Why this company' }]);

	const parsed = ComposeLoop.Parse(
		'```json\n['
		+ '{"field_id":"f3","text":"<b>Because</b> shipping matters"},'
		+ '{"field_id":"f9","text":"ghost field"},'
		+ '{"field_id":"f2","text":"   "},'
		+ '{"field_id":"f2","text":"kept"},'
		+ '{"field_id":"f2","text":"dup"}'
		+ ']\n```',
		fields,
	);

	assert.deepStrictEqual(jsonOf(parsed.drafts.map(d => d.fieldKey)), ['why', 'cover']);
	assert.strictEqual(parsed.drafts[0].text, 'Because shipping matters');
	assert.strictEqual(parsed.drafts[1].text, 'kept');

	assert.strictEqual(ComposeLoop.Parse('Sure, here is your letter', fields).error, 'compose-invalid-json');
	assert.strictEqual(ComposeLoop.Parse('{"x":1}', fields).error, 'compose-invalid-output');

	const wrapped = ComposeLoop.Parse('{ "drafts": [{ "field_id": "f2", "text": "wrapped" }] }', fields);
	assert.strictEqual(wrapped.drafts[0].text, 'wrapped');
});

test('compose run is a single no-tools call; llm failures and empty field lists surface as errors', async () => {
	const { ComposeLoop } = composeEnv();
	const seen = [];
	const client = {
		async Chat(messages, tools) {
			seen.push({ messages: JSON.parse(JSON.stringify(messages)), tools });
			return { content: '[{"field_id":"f2","text":"ok"}]', tool_calls: [] };
		},
	};

	const result = await ComposeLoop.Run({ client, domain: 'd', resume: 'R', guidance: 'g', fields: LONG_FIELDS });
	assert.strictEqual(result.drafts.length, 1);
	assert.strictEqual(seen.length, 1);
	assert.strictEqual(seen[0].tools, null);

	const failed = await ComposeLoop.Run({
		client: { async Chat() { return { error: 'llm-timeout' }; } },
		fields: LONG_FIELDS,
	});
	assert.strictEqual(failed.error, 'llm-timeout');

	const empty = await ComposeLoop.Run({ client, fields: [] });
	assert.strictEqual(empty.error, 'no-long-fields');
});

test('store merge replaces same-field drafts, keeps other domains and caps rows', async () => {
	const { ComposeStore } = composeEnv();

	const first = ComposeStore.Merge([], [{ fieldId: 'f2', fieldKey: 'cover', fieldLabel: 'Cover letter', text: 'one' }], 'ats.example');
	const second = ComposeStore.Merge(first, [{ fieldId: 'f2', fieldKey: 'cover', fieldLabel: 'Cover letter', text: 'two' }], 'ats.example');

	assert.strictEqual(second.filter(d => d.id === 'ats.example::cover').length, 1);
	assert.strictEqual(second.find(d => d.id === 'ats.example::cover').text, 'two');

	const cross = ComposeStore.Merge(second, [{ fieldId: 'f5', fieldKey: 'cover', fieldLabel: 'Cover', text: 'other site' }], 'other.example');
	assert.strictEqual(cross.filter(d => d.fieldKey === 'cover').length, 2);

	const many = ComposeStore.Merge([], Array.from({ length: 25 }, (_, i) => ({
		fieldId: 'f' + i, fieldKey: 'k' + i, fieldLabel: 'k', text: 'x',
	})), 'd');
	assert.ok(many.length <= ComposeStore.MAX_ROWS);
});

test('accept requires text and targets pending drafts; reject removes only existing drafts', async () => {
	const { ComposeStore } = composeEnv();
	await ComposeStore.Save(ComposeStore.Merge([], [{ fieldId: 'f2', fieldKey: 'cover', fieldLabel: 'L', text: 'one' }], 'ats.example'));

	assert.strictEqual((await ComposeStore.Accept('ats.example::cover', '   ')).error, 'validation');
	assert.strictEqual((await ComposeStore.Accept('missing::x', 'text')).error, 'unknown-draft');

	const accepted = await ComposeStore.Accept('ats.example::cover', 'final text');
	assert.strictEqual(accepted.ok, true);
	assert.strictEqual((await ComposeStore.All()).find(d => d.id === 'ats.example::cover').accepted, true);
	assert.strictEqual((await ComposeStore.Accept('ats.example::cover', 'again')).error, 'unknown-draft');

	assert.strictEqual((await ComposeStore.Reject('missing::x')).error, 'unknown-draft');
	assert.strictEqual((await ComposeStore.Reject('ats.example::cover')).ok, true);
	assert.deepStrictEqual(jsonOf(await ComposeStore.All()), []);
});

test('compose drafts stay pending and never touch the page before accept', async () => {
	const { sw, content } = await freshTab();
	sw.fetchStub.route('v1/chat/completions', llmContent('[{"field_id":"f2","text":"Dear hiring team, I mentor."}]'));

	const result = await deliver(sw.chrome, { title: 'compose', params: { tabId: 1, resumeText: 'Ryan' } });

	assert.strictEqual(result.error, undefined);
	assert.strictEqual(result.drafts.length, 1);
	assert.strictEqual(result.drafts[0].accepted, false);
	assert.strictEqual(content.sandbox.document.querySelector('textarea').value, '');

	const stored = sw.chrome.storage.session.state.get('COMPOSE_DRAFTS');
	assert.strictEqual(stored.length, 1);
	assert.strictEqual(stored[0].id, 'ats.example::cover');

	const chat = sw.fetchStub.calls.find(c => c.url.includes('v1/chat/completions'));
	assert.ok(JSON.parse(chat.data.body).tools == null);
});

test('accept then fill applies the accepted draft deterministically', async () => {
	const { sw, content } = await freshTab();
	let turn = 0;
	sw.fetchStub.route('v1/chat/completions', () =>
		turn++ === 0
			? llmContent('[{"field_id":"f2","text":"Dear hiring team."}]')
			: llmContent('done'));

	await deliver(sw.chrome, { title: 'compose', params: { tabId: 1, resumeText: 'Ryan' } });
	await deliver(sw.chrome, { title: 'compose-accept', params: { id: 'ats.example::cover', text: 'Edited accepted text.' } });

	const result = await deliver(sw.chrome, { title: 'fill', params: { tabId: 1, resumeText: 'Ryan' } });

	assert.strictEqual(result.done, true);
	assert.strictEqual(result.drafted, 1);
	assert.strictEqual(content.sandbox.document.querySelector('textarea').value, 'Edited accepted text.');
});

test('unaccepted or rejected drafts never fill; the phase-5 long-text guard stays', async () => {
	const { sw, content } = await freshTab();
	let turn = 0;
	sw.fetchStub.route('v1/chat/completions', () =>
		turn++ === 0
			? llmContent('[{"field_id":"f2","text":"Dear hiring team."}]')
			: { body: { choices: [{ message: { content: '', tool_calls: [{ id: 'a', function: { name: 'fill', arguments: '{"field_id":"f2","value":"I invented this."}' } }] } }] } });

	await deliver(sw.chrome, { title: 'compose', params: { tabId: 1, resumeText: 'Ryan' } });

	const pending = await deliver(sw.chrome, { title: 'fill', params: { tabId: 1, resumeText: 'Ryan' } });
	assert.strictEqual(pending.filled, 0);
	assert.strictEqual(pending.drafted, 0);
	assert.strictEqual(content.sandbox.document.querySelector('textarea').value, '');

	await deliver(sw.chrome, { title: 'compose-reject', params: { id: 'ats.example::cover' } });
	turn = 0;
	sw.fetchStub.route('v1/chat/completions', () => llmContent('done'));

	const rejected = await deliver(sw.chrome, { title: 'fill', params: { tabId: 1, resumeText: 'Ryan' } });
	assert.strictEqual(rejected.drafted, 0);
	assert.strictEqual(content.sandbox.document.querySelector('textarea').value, '');
});

test('fill without compose still leaves long textareas empty (phase-5 guard)', async () => {
	const { sw, content } = await freshTab();
	let turn = 0;
	sw.fetchStub.route('v1/chat/completions', () =>
		turn++ === 0
			? { body: { choices: [{ message: { content: '', tool_calls: [{ id: 'a', function: { name: 'fill', arguments: '{"field_id":"f2","value":"I invented this cover letter."}' } }] } }] } }
			: llmContent('done'));

	const result = await deliver(sw.chrome, { title: 'fill', params: { tabId: 1, resumeText: 'Ryan' } });

	assert.strictEqual(result.done, true);
	assert.strictEqual(result.filled, 0);
	assert.strictEqual(result.drafted, 0);
	assert.strictEqual(content.sandbox.document.querySelector('textarea').value, '');
});

test('compose without long-text fields answers no-long-fields', async () => {
	const { sw } = await freshTab('<input type="text" name="email">');

	const result = await deliver(sw.chrome, { title: 'compose', params: { tabId: 1, resumeText: 'Ryan' } });

	assert.deepStrictEqual(jsonOf(result), { error: 'no-long-fields' });
});
