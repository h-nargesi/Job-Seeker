import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh(html) {
	const env = createEnv({ dom: true, url: 'https://ats.example/apply' });
	env.sandbox.document.body.innerHTML = html;
	env.load('controllers/form-inventory.js');
	env.load('controllers/fill-handler.js');
	return { env, FormInventory: env.grab('FormInventory'), FillHandler: env.grab('FillHandler') };
}

test('fill sets the value and fires bubbling input and change events', async () => {
	const { env, FormInventory, FillHandler } = fresh('<input type="text" name="email">');
	FormInventory.Extract();

	const doc = env.sandbox.document;
	const seen = [];
	doc.addEventListener('input', () => seen.push('input'));
	doc.addEventListener('change', () => seen.push('change'));

	const result = FillHandler.Apply('f1', 'ryan@example.com');
	assert.deepStrictEqual(jsonOf(result), { ok: true });
	assert.strictEqual(doc.querySelector('input').value, 'ryan@example.com');
	assert.deepStrictEqual(seen, ['input', 'change']);
});

test('fill writes through the prototype value setter so framework state stays in sync', async () => {
	const { env, FormInventory, FillHandler } = fresh('<input type="text" name="q">');
	FormInventory.Extract();

	const input = env.sandbox.document.querySelector('input');
	const proto = env.sandbox.window.HTMLInputElement.prototype;
	const original = Object.getOwnPropertyDescriptor(proto, 'value');

	let spy = null;
	Object.defineProperty(proto, 'value', {
		get: original.get,
		set(v) { spy = v; original.set.call(this, v); },
		configurable: true,
	});

	FillHandler.Apply('f1', 'abc');

	assert.strictEqual(spy, 'abc');
	assert.strictEqual(original.get.call(input), 'abc');
});

test('checkbox fill checks for truthy values and unchecks otherwise', async () => {
	const { env, FormInventory, FillHandler } = fresh('<input type="checkbox" name="tos">');
	FormInventory.Extract();

	const box = env.sandbox.document.querySelector('input');
	FillHandler.Apply('f1', 'true');
	assert.strictEqual(box.checked, true);
	FillHandler.Apply('f1', 'no');
	assert.strictEqual(box.checked, false);
});

test('select fill matches by value, then by option text, else reports no-option', async () => {
	const { env, FormInventory, FillHandler } = fresh(`
		<select name="country">
			<option value="">Choose</option>
			<option value="de">Germany</option>
		</select>
	`);
	FormInventory.Extract();

	const select = env.sandbox.document.querySelector('select');
	assert.deepStrictEqual(jsonOf(FillHandler.Apply('f1', 'de')), { ok: true });
	assert.strictEqual(select.value, 'de');
	assert.deepStrictEqual(jsonOf(FillHandler.Apply('f1', 'germany')), { ok: true });
	assert.strictEqual(select.value, 'de');
	assert.deepStrictEqual(jsonOf(FillHandler.Apply('f1', 'atlantis')), { ok: false, error: 'no-option' });
});

test('radio fill checks the matching option only', async () => {
	const { env, FormInventory, FillHandler } = fresh(`
		<input type="radio" name="visa" value="yes">
		<input type="radio" name="visa" value="no">
	`);
	FormInventory.Extract();

	assert.deepStrictEqual(jsonOf(FillHandler.Apply('f1', 'no')), { ok: true });
	const radios = env.sandbox.document.querySelectorAll('input');
	assert.strictEqual(radios[0].checked, false);
	assert.strictEqual(radios[1].checked, true);
	assert.deepStrictEqual(jsonOf(FillHandler.Apply('f1', 'maybe')), { ok: false, error: 'no-option' });
});

test('file inputs and unknown ids are refused', async () => {
	const { FormInventory, FillHandler } = fresh('<input type="file" name="cv"><input type="text" name="a">');
	FormInventory.Extract();

	assert.deepStrictEqual(jsonOf(FillHandler.Apply('f1', 'x.pdf')), { ok: false, error: 'manual' });
	assert.deepStrictEqual(jsonOf(FillHandler.Apply('f99', 'x')), { ok: false, error: 'unknown-field' });
});

test('diffs report only fields the assistant filled whose final value changed', async () => {
	const { env, FormInventory, FillHandler } = fresh('<input type="text" name="email"><input type="text" name="city">');
	FormInventory.Extract();

	FillHandler.Apply('f1', 'ryan@example.com');
	FillHandler.Apply('f2', 'Berlin');

	env.sandbox.document.querySelectorAll('input')[0].value = 'ryan2@example.com';

	const diffs = jsonOf(FillHandler.Diffs());
	assert.strictEqual(diffs.length, 1);
	assert.deepStrictEqual(diffs[0], {
		fieldKey: 'email',
		fieldLabel: '',
		aiValue: 'ryan@example.com',
		finalValue: 'ryan2@example.com',
	});
});

test('reset clears the recorded fills so old sessions never diff again', async () => {
	const { env, FormInventory, FillHandler } = fresh('<input type="text" name="email">');
	FormInventory.Extract();

	FillHandler.Apply('f1', 'ryan@example.com');
	env.sandbox.document.querySelector('input').value = 'other';
	FillHandler.Reset();

	assert.strictEqual(FillHandler.Diffs().length, 0);
});
