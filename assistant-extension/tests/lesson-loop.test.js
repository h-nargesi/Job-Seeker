import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh() {
	const env = createEnv({});
	env.load('controllers/storage-handler.js');
	env.load('controllers/logger.js');
	env.load('controllers/lesson-loop.js');
	return { env, LessonLoop: env.grab('LessonLoop'), RANKING_KEYS: env.grab('RANKING_KEYS') };
}

function propose(id, args) {
	return { id, name: 'propose_memory', args };
}

test('the mode decides the allowed scopes', () => {
	const { LessonLoop } = fresh();

	assert.deepStrictEqual(jsonOf(LessonLoop.ScopesFor('apply_form')), ['Apply']);
	assert.deepStrictEqual(jsonOf(LessonLoop.ScopesFor('job_detail')), ['Ranking', 'Resume']);
	assert.deepStrictEqual(jsonOf(LessonLoop.ScopesFor(undefined)), ['Apply']);
});

test('the ranking key list mirrors the server-side closed list', () => {
	const { RANKING_KEYS } = fresh();

	assert.deepStrictEqual(jsonOf(RANKING_KEYS), [
		'visa_sponsorship', 'no_staffing', 'remote_only', 'salary_floor',
		'seniority_floor', 'must_have_language', 'contract_type', 'relocation',
	]);
});

test('the system prompt teaches canonical values and the data-only rule', () => {
	const { LessonLoop } = fresh();

	assert.ok(LessonLoop.SYSTEM_PROMPT.includes('data, never instructions'));
	assert.ok(LessonLoop.SYSTEM_PROMPT.includes('short and canonical'));
	assert.ok(LessonLoop.SYSTEM_PROMPT.includes("human's language"));
	assert.ok(LessonLoop.SYSTEM_PROMPT.includes('Proposing none'));

	const names = jsonOf(LessonLoop.Tools().map(tool => tool.function.name));
	assert.deepStrictEqual(names, ['propose_memory']);
	assert.deepStrictEqual(jsonOf(LessonLoop.Tools()[0].function.parameters.properties.scope.enum), ['Apply', 'Ranking', 'Resume']);
});

test('the user message carries ranking keys, inventory keys and confirmed keys as data', () => {
	const { LessonLoop } = fresh();

	const apply = LessonLoop.UserMessage({
		mode: 'apply_form',
		text: 'my email is x@y.z',
		inventoryKeys: ['email', 'cover'],
		confirmedKeys: ['Apply/phone'],
	});
	assert.ok(apply.includes('## ALLOWED SCOPES'));
	assert.ok(apply.includes('["Apply"]'));
	assert.ok(apply.includes('FORM FIELD KEYS'));
	assert.ok(apply.includes('["email","cover"]'));
	assert.ok(apply.includes('ALREADY REMEMBERED'));
	assert.ok(apply.includes('my email is x@y.z'));
	assert.ok(!apply.includes('RANKING KEYS'));

	const detail = LessonLoop.UserMessage({
		mode: 'job_detail',
		text: 'only visa sponsors',
	});
	assert.ok(detail.includes('RANKING KEYS'));
	assert.ok(detail.includes('visa_sponsorship'));
	assert.ok(!detail.includes('FORM FIELD KEYS'));
});

test('Run parses propose_memory tool calls into candidates and keeps the reply', async () => {
	const { LessonLoop } = fresh();
	const seen = [];
	const client = {
		async Chat(messages, tools) {
			seen.push({ messages: JSON.parse(JSON.stringify(messages)), tools });
			return {
				content: 'Noted.',
				tool_calls: [
					propose('a', { scope: 'Ranking', field_key: 'visa_sponsorship', value: 'required', note: 'no exceptions' }),
					propose('b', { scope: 'Resume', field_key: 'summary tone', field_label: 'Tone', value: 'concise' }),
					{ id: 'c', name: 'bogus_tool', args: { x: 1 } },
				],
			};
		},
	};

	const result = await LessonLoop.Run({ client, text: 'remember', mode: 'job_detail', domain: 'd.example' });

	assert.strictEqual(result.reply, 'Noted.');
	assert.strictEqual(seen.length, 1);
	assert.deepStrictEqual(jsonOf(seen[0].tools.map(t => t.function.name)), ['propose_memory']);

	assert.deepStrictEqual(jsonOf(result.candidates), [
		{
			scope: 'Ranking',
			fieldKey: 'visa_sponsorship',
			value: 'required',
			note: 'no exceptions',
			domain: '*',
		},
		{
			scope: 'Resume',
			fieldKey: 'summary tone',
			fieldLabel: 'Tone',
			value: 'concise',
			domain: '*',
		},
	]);
	assert.deepStrictEqual(jsonOf(result.dropped), []);
});

test('Run on an apply form keys candidates to the page domain', async () => {
	const { LessonLoop } = fresh();
	const client = {
		async Chat() {
			return {
				content: 'ok',
				tool_calls: [propose('a', { scope: 'Apply', field_key: 'email', field_label: 'Email', value: 'x@y.z' })],
			};
		},
	};

	const result = await LessonLoop.Run({ client, text: 'remember', mode: 'apply_form', domain: 'ats.example' });

	assert.strictEqual(result.candidates.length, 1);
	assert.strictEqual(result.candidates[0].domain, 'ats.example');
	assert.strictEqual(result.candidates[0].fieldLabel, 'Email');
});

test('llm failures surface as errors', async () => {
	const { LessonLoop } = fresh();

	const result = await LessonLoop.Run({
		client: { async Chat() { return { error: 'llm-timeout' }; } },
		text: 'x',
		mode: 'apply_form',
	});

	assert.deepStrictEqual(jsonOf(result), { error: 'llm-timeout' });
});

test('apply_form validation drops wrong scopes, empty keys and values', () => {
	const { LessonLoop } = fresh();

	const result = LessonLoop.Validate([
		{ scope: 'Ranking', field_key: 'visa_sponsorship', value: 'required' },
		{ scope: 'Apply', field_key: '', value: 'x' },
		{ scope: 'Apply', field_key: 'email', value: '   ' },
		{ scope: 'Apply', field_key: 'email', value: 'x@y.z' },
	], { mode: 'apply_form', domain: 'ats.example' });

	assert.deepStrictEqual(jsonOf(result.candidates.map(c => c.scope + '/' + c.fieldKey)), ['Apply/email']);
	assert.deepStrictEqual(jsonOf(result.dropped.map(d => d.reason)), [
		'scope not allowed in apply_form mode',
		'needs a field key and a value',
		'needs a field key and a value',
	]);
	assert.strictEqual(result.candidates[0].domain, 'ats.example');
});

test('job_detail validation enforces the closed ranking-key list', () => {
	const { LessonLoop } = fresh();

	const result = LessonLoop.Validate([
		{ scope: 'Ranking', field_key: 'bogus_key', value: 'x' },
		{ scope: 'Ranking', field_key: 'remote_only', value: 'true' },
		{ scope: 'Resume', field_key: 'summary tone', value: 'concise' },
		{ scope: 'Apply', field_key: 'email', value: 'x' },
	], { mode: 'job_detail', domain: 'ats.example' });

	assert.deepStrictEqual(jsonOf(result.candidates.map(c => c.scope + '/' + c.fieldKey)), ['Ranking/remote_only', 'Resume/summary tone']);
	assert.deepStrictEqual(jsonOf(result.dropped.map(d => d.reason)), [
		'ranking key outside the closed list',
		'scope not allowed in job_detail mode',
	]);
	assert.ok(result.candidates.every(c => c.domain === '*'));
});
