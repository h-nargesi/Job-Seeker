import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, link, deliver, jsonOf } from './helpers/env.js';

async function fresh(contentHtml = '<input type="text" name="email">') {
	const sw = createEnv({ importScripts: true });
	const content = createEnv({ dom: true, url: 'https://ats.example/apply?jobid=12' });

	content.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081/');
	sw.chrome.storage.local.state.set('SERVER_URL', 'https://core.example:8081/');
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
	content.sandbox.document.body.innerHTML = contentHtml;

	return { sw, content };
}

function chatResponse(tool_calls, content_text) {
	return {
		choices: [{
			message: {
				content: content_text ?? '',
				tool_calls: (tool_calls || []).map(call => ({
					id: call.id,
					function: { name: call.name, arguments: JSON.stringify(call.args) },
				})),
			},
		}],
	};
}

function routeCore(sw, options = {}) {
	sw.fetchStub.route('assistant/jobs', { body: options.jobs ?? [] });
	sw.fetchStub.route(/assistant\/memory\?/, {
		body: options.memory ?? [],
	});
	sw.fetchStub.route('assistant/memorysave', { body: { id: 1 } });
	sw.fetchStub.route('assistant/memorybump', { body: {} });
}

test('jobs requests proxy straight through to the core', async () => {
	const { sw } = await fresh();
	routeCore(sw, { jobs: [{ jobId: 5, title: 'Dev', pendingProposal: false }] });

	const result = await deliver(sw.chrome, { title: 'jobs' });

	assert.deepStrictEqual(jsonOf(result), [{ jobId: 5, title: 'Dev', pendingProposal: false }]);
	assert.ok(sw.fetchStub.calls.some(c => c.url.includes('assistant/jobs')));
});

test('a full fill session: inventory, memory query, fill, bump — and no decision calls', async () => {
	const { sw, content } = await fresh();

	routeCore(sw, {
		memory: [{ memoryID: 7, scope: 'Apply', fieldKey: 'email', value: 'ryan@example.com', kind: 'Tip', agencyDomain: '*', confirmed: true, useCount: 1 }],
	});

	const turns = [
		chatResponse([{ id: 'a', name: 'memory_query', args: { field_key: 'email' } }]),
		chatResponse([{ id: 'b', name: 'fill', args: { field_id: 'f1', value: 'ryan@example.com' } }]),
		chatResponse([], 'done filling'),
	];
	let turn = 0;
	sw.fetchStub.route('v1/chat/completions', () => chatBody(turns[turn++]));

	const result = await deliver(sw.chrome, {
		title: 'fill',
		params: { tabId: 1, jobId: 12, resumeText: 'Ryan, .NET developer.' },
	});

	assert.strictEqual(result.done, true);
	assert.strictEqual(result.filled, 1);
	assert.strictEqual(content.sandbox.document.querySelector('input').value, 'ryan@example.com');

	const urls = sw.fetchStub.calls.map(c => c.url);
	assert.ok(urls.some(u => u.includes('assistant/memory?scope=Apply&confirmed=true')));
	assert.ok(urls.some(u => u.includes('assistant/memorybump?id=7')));
	assert.ok(urls.some(u => u.includes('v1/chat/completions')));
	for (const url of urls) assert.ok(!url.includes('/decision/'), url);
});

test('fill without a content script answers no-content-script', async () => {
	const { sw } = await fresh();
	sw.chrome.tabs.forward = null;

	const result = await deliver(sw.chrome, { title: 'fill', params: { tabId: 99, resumeText: '' } });

	assert.deepStrictEqual(jsonOf(result), { error: 'no-content-script' });
});

test('chat lessons are stored confirmed and echoed into the session chat log', async () => {
	const { sw } = await fresh();
	routeCore(sw);

	const result = await deliver(sw.chrome, {
		title: 'tip',
		params: { scope: 'Ranking', domain: '*', fieldKey: 'visa_sponsorship', value: 'required' },
	});

	assert.deepStrictEqual(jsonOf(result), { id: 1 });
	assert.deepStrictEqual(JSON.parse(sw.fetchStub.calls.find(c => c.url.includes('memorysave')).data.body), {
		scope: 'Ranking',
		domain: '*',
		fieldKey: 'visa_sponsorship',
		kind: 'Tip',
		confirmed: true,
		value: 'required',
	});

	const log = sw.chrome.storage.session.state.get('CHAT_LOG');
	assert.strictEqual(log.length, 1);
	assert.strictEqual(log[0].fieldKey, 'visa_sponsorship');
});

test('flush-diffs drains the queued submit corrections as unconfirmed rows', async () => {
	const { sw } = await fresh();
	routeCore(sw);

	await sw.chrome.storage.session.set({
		PENDING_DIFFS: [{
			domain: 'ats.example',
			fieldKey: 'email',
			fieldLabel: 'Email',
			aiValue: 'ai@example.com',
			finalValue: 'human@example.com',
		}],
	});

	const result = await deliver(sw.chrome, { title: 'flush-diffs' });

	assert.deepStrictEqual(jsonOf(result), { flushed: 1 });
	assert.deepStrictEqual(JSON.parse(sw.fetchStub.calls.find(c => c.url.includes('memorysave')).data.body), {
		scope: 'Apply',
		domain: 'ats.example',
		fieldKey: 'email',
		fieldLabel: 'Email',
		kind: 'Correction',
		confirmed: false,
		value: 'human@example.com',
		note: 'ai filled: ai@example.com',
	});
	assert.deepStrictEqual(jsonOf(sw.chrome.storage.session.state.get('PENDING_DIFFS')), []);
});

test('unknown titles answer with an error object', async () => {
	const { sw } = await fresh();
	const result = await deliver(sw.chrome, { title: 'bogus' });

	assert.deepStrictEqual(jsonOf(result), { error: 'unknown-title' });
});

function chatBody(turn) {
	return { body: turn };
}
