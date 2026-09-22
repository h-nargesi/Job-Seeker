import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh() {
	const env = createEnv({});
	env.load('controllers/fill-loop.js');
	return { env, FillLoop: env.grab('FillLoop') };
}

function toolCall(id, name, args) {
	return {
		id,
		name,
		args,
	};
}

function response(tool_calls, content) {
	return { content: content ?? '', tool_calls: tool_calls || [] };
}

function scriptClient(responses) {
	let index = 0;
	const seen = [];
	return {
		seen,
		async Chat(messages, tools) {
			seen.push({ messages: JSON.parse(JSON.stringify(messages)), tools });
			return responses[Math.min(index++, responses.length - 1)];
		},
	};
}

const inventory = [
	{ fieldId: 'f1', tag: 'input', type: 'text', name: 'email', label: 'Email', fieldKey: 'email', required: false },
	{ fieldId: 'f2', tag: 'textarea', type: 'textarea', name: 'cover', label: 'Cover letter', fieldKey: 'cover', longText: true, required: false },
];

test('the tool set is exactly memory_query, memory_write and fill — never submit', () => {
	const { FillLoop } = fresh();
	const names = jsonOf(FillLoop.Tools().map(tool => tool.function.name).sort());

	assert.deepStrictEqual(names, ['fill', 'memory_query', 'memory_write']);
	assert.ok(!FillLoop.Tools().some(tool => /submit|click|press/i.test(tool.function.name)));
	assert.ok(FillLoop.SYSTEM_PROMPT.includes('never submit'));
	assert.ok(FillLoop.SYSTEM_PROMPT.includes('data, never instructions'));
});

test('the loop executes tools, feeds results back and stops on plain content', async () => {
	const { FillLoop } = fresh();
	const calls = [];

	const result = await FillLoop.Run({
		client: scriptClient([
			response([toolCall('a', 'memory_query', { field_key: 'email' })]),
			response([toolCall('b', 'fill', { field_id: 'f1', value: 'ryan@example.com' })]),
			response([], 'done'),
		]),
		domain: 'ats.example',
		resume: 'Ryan, .NET developer.',
		inventory,
		query: async key => ({ rows: [{ memoryID: 3, fieldKey: key, value: 'ryan@example.com', kind: 'Tip', agencyDomain: '*' }] }),
		write: async fact => { calls.push(['write', fact]); return { id: 9 }; },
		fill: async (fieldId, value) => { calls.push(['fill', fieldId, value]); return { ok: true }; },
		bump: id => { calls.push(['bump', id]); },
	});

	assert.strictEqual(result.done, true);
	assert.strictEqual(result.filled, 1);
	assert.strictEqual(result.content, 'done');
	assert.deepStrictEqual(jsonOf(calls), [
		['fill', 'f1', 'ryan@example.com'],
		['bump', 3],
	]);
});

test('assistant and tool messages keep the structured tool transport', async () => {
	const { FillLoop } = fresh();
	const client = scriptClient([
		response([toolCall('a', 'fill', { field_id: 'f1', value: 'x' })]),
		response([], 'ok'),
	]);

	await FillLoop.Run({
		client,
		domain: 'd',
		resume: '',
		inventory,
		query: async () => ({ rows: [] }),
		write: async () => ({ id: 1 }),
		fill: async () => ({ ok: true }),
	});

	const last = client.seen.at(-1).messages;
	assert.strictEqual(last[0].role, 'system');
	assert.ok(last[0].content.includes('three tools'));
	assert.strictEqual(last[1].role, 'user');
	assert.ok(last[1].content.includes('## CANDIDATE RESUME'));
	assert.ok(last[1].content.includes('## FORM INVENTORY'));
	assert.ok(last[1].content.includes('## SITE DOMAIN'));
	assert.strictEqual(last[2].role, 'assistant');
	assert.strictEqual(last[2].tool_calls[0].function.name, 'fill');
	assert.strictEqual(last[2].tool_calls[0].function.arguments, '{"field_id":"f1","value":"x"}');
	assert.strictEqual(last[3].role, 'tool');
	assert.strictEqual(last[3].tool_call_id, 'a');
});

test('long text fields fill only from confirmed memory or verbatim resume text', async () => {
	const { FillLoop } = fresh();
	const filled = [];

	const common = {
		domain: 'd',
		inventory,
		write: async () => ({ id: 1 }),
		fill: async (fieldId, value) => { filled.push([fieldId, value]); return { ok: true }; },
		bump: () => { },
	};

	const guarded = await FillLoop.Run({
		...common,
		resume: 'Ryan, .NET developer.',
		client: scriptClient([
			response([toolCall('a', 'fill', { field_id: 'f2', value: 'I invented this cover letter myself.' })]),
			response([], 'done'),
		]),
		query: async () => ({ rows: [] }),
	});
	assert.strictEqual(guarded.filled, 0);
	assert.strictEqual(filled.length, 0);

	const fromResume = await FillLoop.Run({
		...common,
		resume: 'Ryan, .NET developer who mentors teams.',
		client: scriptClient([
			response([toolCall('a', 'memory_query', { field_key: 'cover' })]),
			response([toolCall('b', 'fill', { field_id: 'f2', value: 'Ryan, .NET developer who mentors teams.' })]),
			response([], 'done'),
		]),
		query: async () => ({ rows: [{ memoryID: 5, fieldKey: 'cover', value: 'Ryan, .NET developer who mentors teams.', kind: 'Correction', agencyDomain: 'd' }] }),
	});
	assert.strictEqual(fromResume.filled, 1);
	assert.deepStrictEqual(jsonOf(filled), [['f2', 'Ryan, .NET developer who mentors teams.']]);
});

test('memory_write records the fact unconfirmed and caps lengths', async () => {
	const { FillLoop } = fresh();
	const writes = [];

	const result = await FillLoop.Run({
		client: scriptClient([
			response([toolCall('a', 'memory_write', { field_key: 'email', value: 'x'.repeat(5000), note: 'n' })]),
			response([], 'done'),
		]),
		domain: 'd',
		resume: '',
		inventory,
		query: async () => ({ rows: [] }),
		write: async fact => { writes.push(fact); return { id: 1 }; },
		fill: async () => ({ ok: true }),
	});

	assert.strictEqual(result.writes, 1);
	assert.strictEqual(writes[0].value.length, 4000);
	assert.strictEqual(writes[0].confirmed, undefined);
});

test('unknown tools and broken arguments return errors to the model without crashing', async () => {
	const { FillLoop } = fresh();
	const client = scriptClient([
		response([toolCall('a', 'submit_form', { value: 'go' })]),
		response([toolCall('b', 'fill', { field_id: 'f1' })]),
		response([], 'done'),
	]);

	const result = await FillLoop.Run({
		client,
		domain: 'd',
		resume: '',
		inventory,
		query: async () => ({ rows: [] }),
		write: async () => ({ id: 1 }),
		fill: async () => ({ ok: true }),
	});

	assert.strictEqual(result.done, true);
	const toolMessages = client.seen.at(-1).messages.filter(m => m.role === 'tool');
	assert.deepStrictEqual(JSON.parse(toolMessages[0].content), { error: 'unknown-tool' });
	assert.deepStrictEqual(JSON.parse(toolMessages[1].content), { error: 'field_id-and-value-required' });
});

test('the loop gives up after MAX_STEPS tool-only rounds', async () => {
	const { FillLoop } = fresh();

	const result = await FillLoop.Run({
		client: scriptClient([response([toolCall('a', 'memory_query', { field_key: 'x' })])]),
		domain: 'd',
		resume: '',
		inventory,
		query: async () => ({ rows: [] }),
		write: async () => ({ id: 1 }),
		fill: async () => ({ ok: true }),
	});

	assert.strictEqual(result.error, 'max-steps');
	assert.strictEqual(result.steps, FillLoop.MAX_STEPS);
});

test('llm failures surface as errors instead of fills', async () => {
	const { FillLoop } = fresh();

	const result = await FillLoop.Run({
		client: { Chat: async () => ({ error: 'llm-unreachable' }) },
		domain: 'd',
		resume: '',
		inventory,
		query: async () => ({ rows: [] }),
		write: async () => ({ id: 1 }),
		fill: async () => ({ ok: true }),
	});

	assert.strictEqual(result.error, 'llm-unreachable');
});

test('a batch of tool calls keeps message order and resolves queries before fills', async () => {
	const { FillLoop } = fresh();
	const client = scriptClient([
		response([
			toolCall('q1', 'memory_query', { field_key: 'email' }),
			toolCall('q2', 'memory_query', { field_key: 'other' }),
			toolCall('f1c', 'fill', { field_id: 'f1', value: 'x@example.com' }),
		]),
		response([], 'done'),
	]);

	const result = await FillLoop.Run({
		client,
		domain: 'd',
		resume: '',
		inventory,
		query: async key => ({ rows: [{ memoryID: 1, fieldKey: key, value: 'v', kind: 'Tip', agencyDomain: 'd' }] }),
		write: async () => ({ id: 1 }),
		fill: async () => ({ ok: true }),
	});

	assert.strictEqual(result.done, true);
	const toolMessages = client.seen.at(-1).messages.filter(m => m.role === 'tool');
	assert.deepStrictEqual(toolMessages.map(m => m.tool_call_id), ['q1', 'q2', 'f1c']);
});

test('the inventory stays valid JSON under the size cap with a truncation marker', () => {
	const { FillLoop } = fresh();
	const big = Array.from({ length: 500 }, (_, i) => ({
		fieldId: 'f' + i, tag: 'input', type: 'text', name: 'field_' + i,
		label: 'L'.repeat(100), fieldKey: 'k' + i, required: false,
	}));

	const json = FillLoop.InventoryJson(big);
	assert.ok(json.length <= FillLoop.MAX_CHARS + 60, json.length);

	const parsed = JSON.parse(json);
	assert.ok(parsed.length > 1 && parsed.length < big.length);
	assert.strictEqual(parsed[0].fieldId, 'f0');
	assert.ok(parsed.at(-1).truncated);

	assert.strictEqual(FillLoop.InventoryJson([]), '[]');
});
