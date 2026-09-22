import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh(rows) {
	const env = createEnv({});
	env.load('controllers/memory-tools.js');

	const posted = [];
	const fake = {
		async MemoryList(scope, confirmed) {
			fake.listed = { scope, confirmed };
			return rows;
		},
		async MemorySave(row) {
			posted.push(row);
			return { id: posted.length };
		},
		async MemoryBump(id) {
			fake.bumped = id;
			return {};
		},
		listed: null,
		bumped: null,
		posted,
	};

	return { env, fake, MemoryTools: env.grab('MemoryTools') };
}

const ROWS = [
	{ memoryID: 1, scope: 'Apply', fieldKey: 'email', value: 'tip@example.com', kind: 'Tip', agencyDomain: 'ats.example', confirmed: true, useCount: 0 },
	{ memoryID: 2, scope: 'Apply', fieldKey: 'email', value: 'correction@example.com', kind: 'Correction', agencyDomain: 'ats.example', confirmed: true, useCount: 0 },
	{ memoryID: 3, scope: 'Apply', fieldKey: 'email', value: 'global@example.com', kind: 'Tip', agencyDomain: '*', confirmed: true, useCount: 9 },
	{ memoryID: 4, scope: 'Apply', fieldKey: 'phone', value: '123', kind: 'Tip', agencyDomain: '*', confirmed: true, useCount: 1 },
];

test('query returns confirmed rows for the field with correction-first precedence', async () => {
	const { fake, MemoryTools } = fresh(ROWS);
	const result = await MemoryTools.Query(fake, 'ats.example', 'email');

	assert.deepStrictEqual(jsonOf(result.rows.map(r => r.memoryID)), [2, 1, 3]);
});

test('precedence prefers exact domain over global and higher use counts', async () => {
	const rows = [
		{ memoryID: 1, fieldKey: 'x', value: 'a', kind: 'Tip', agencyDomain: '*', confirmed: true, useCount: 99 },
		{ memoryID: 2, fieldKey: 'x', value: 'b', kind: 'Tip', agencyDomain: 'site.io', confirmed: true, useCount: 1 },
	];
	const { fake, MemoryTools } = fresh(rows);

	const result = await MemoryTools.Query(fake, 'site.io', 'x');
	assert.deepStrictEqual(jsonOf(result.rows.map(r => r.memoryID)), [2, 1]);
});

test('query surfaces server errors instead of an empty answer', async () => {
	const { MemoryTools } = fresh({ error: 'unauthorized', status: 401 });
	const result = await MemoryTools.Query({ MemoryList: async () => ({ error: 'unauthorized', status: 401 }) }, 'd', 'x');

	assert.strictEqual(result.error, 'unauthorized');
	assert.deepStrictEqual(jsonOf(result.rows), []);
});

test('snapshot serves the whole fill run from one list fetch and retries after failures', async () => {
	const { MemoryTools } = fresh(ROWS);
	let calls = 0;
	const fake = { MemoryList: async () => { calls++; return ROWS; } };

	const load = MemoryTools.Snapshot(fake);
	const [a, b] = await Promise.all([load(), load()]);
	assert.strictEqual(calls, 1);
	await load();
	assert.strictEqual(calls, 1);
	assert.deepStrictEqual(jsonOf(a), jsonOf(b));

	let failures = 0;
	const failing = { MemoryList: async () => { failures++; return { error: 'timeout', status: 0 }; } };
	const retry = MemoryTools.Snapshot(failing);
	assert.strictEqual((await retry()).error, 'timeout');
	assert.strictEqual((await retry()).error, 'timeout');
	assert.strictEqual(failures, 2);
});

test('query reads from a snapshot loader instead of hitting the wire per field', async () => {
	const { MemoryTools } = fresh(ROWS);
	let calls = 0;
	const fake = { MemoryList: async () => { calls++; return ROWS; } };
	const load = MemoryTools.Snapshot(fake);

	const email = await MemoryTools.Query(fake, 'ats.example', 'email', load);
	const phone = await MemoryTools.Query(fake, 'ats.example', 'phone', load);

	assert.strictEqual(calls, 1);
	assert.deepStrictEqual(jsonOf(email.rows.map(r => r.memoryID)), [2, 1, 3]);
	assert.deepStrictEqual(jsonOf(phone.rows.map(r => r.memoryID)), [4]);
});

test('apply facts from the fill loop are stored as unconfirmed tips', async () => {
	const { fake, MemoryTools } = fresh([]);
	await MemoryTools.SaveApplyFact(fake, 'ats.example', 'email', 'Email', 'ryan@example.com', 'from resume');

	assert.deepStrictEqual(jsonOf(fake.posted[0]), {
		scope: 'Apply',
		domain: 'ats.example',
		fieldKey: 'email',
		fieldLabel: 'Email',
		kind: 'Tip',
		confirmed: false,
		value: 'ryan@example.com',
		note: 'from resume',
	});
});

test('submit diffs are stored as unconfirmed corrections', async () => {
	const { fake, MemoryTools } = fresh([]);
	await MemoryTools.SaveCorrection(fake, {
		domain: 'ats.example',
		fieldKey: 'email',
		fieldLabel: 'Email',
		aiValue: 'ai@example.com',
		finalValue: 'human@example.com',
	});

	assert.deepStrictEqual(jsonOf(fake.posted[0]), {
		scope: 'Apply',
		domain: 'ats.example',
		fieldKey: 'email',
		fieldLabel: 'Email',
		kind: 'Correction',
		confirmed: false,
		value: 'human@example.com',
		note: 'ai filled: ai@example.com',
	});
});

test('chat lessons are stored as confirmed tips', async () => {
	const { fake, MemoryTools } = fresh([]);
	await MemoryTools.SaveTip(fake, {
		scope: 'Ranking',
		domain: '*',
		fieldKey: 'visa_sponsorship',
		value: 'required',
		note: 'no visa, no apply',
	});

	assert.deepStrictEqual(jsonOf(fake.posted[0]), {
		scope: 'Ranking',
		domain: '*',
		fieldKey: 'visa_sponsorship',
		kind: 'Tip',
		confirmed: true,
		value: 'required',
		note: 'no visa, no apply',
	});
});
