import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh(html) {
	const env = createEnv({ dom: true, url: 'https://ats.example/apply' });
	env.sandbox.document.body.innerHTML = html;
	env.load('controllers/form-inventory.js');
	return { env, FormInventory: env.grab('FormInventory') };
}

test('extracts controls with stable field ids and no DOM references', () => {
	const { FormInventory } = fresh(`
		<label for="fn">First Name:</label>
		<input id="fn" type="text" name="first_name" required />
		<textarea name="cover"></textarea>
		<select name="country"><option value="">Choose</option><option value="de">Germany</option></select>
		<input type="file" name="cv" />
		<input type="hidden" name="csrf" />
		<input type="submit" value="Send" />
		<input type="text" name="off" disabled />
	`);
	const inventory = FormInventory.Extract();
	const round = jsonOf(inventory);

	assert.deepStrictEqual(round.map(e => e.fieldId), ['f1', 'f2', 'f3', 'f4']);
	assert.strictEqual(round[0].tag, 'input');
	assert.strictEqual(round[0].type, 'text');
	assert.strictEqual(round[0].label, 'First Name:');
	assert.strictEqual(round[0].fieldKey, 'first_name');
	assert.strictEqual(round[0].required, true);
	assert.strictEqual(round[1].longText, true);
	assert.deepStrictEqual(round[2].options, [
		{ value: '', text: 'Choose' },
		{ value: 'de', text: 'Germany' },
	]);
	assert.strictEqual(round[3].manual, true);
});

test('hidden, submit and disabled controls never reach the model', () => {
	const { FormInventory } = fresh('<input type="hidden" name="h"><input type="submit"><input name="d" disabled>');
	assert.strictEqual(FormInventory.Extract().length, 0);
});

test('radio group collapses into one entry with options', () => {
	const { FormInventory } = fresh(`
		<fieldset>
			<legend>Visa required</legend>
			<input type="radio" id="r1" name="visa" value="yes" /><label for="r1">Yes</label>
			<input type="radio" id="r2" name="visa" value="no" /><label for="r2">No</label>
		</fieldset>
	`);
	const inventory = FormInventory.Extract();

	assert.strictEqual(inventory.length, 1);
	assert.strictEqual(inventory[0].type, 'radio');
	assert.strictEqual(inventory[0].label, 'Visa required');
	assert.deepStrictEqual(jsonOf(inventory[0].options), [
		{ value: 'yes', text: 'Yes' },
		{ value: 'no', text: 'No' },
	]);
});

test('checkbox entries expose true and false options', () => {
	const { FormInventory } = fresh('<input type="checkbox" name="tos" aria-label="Accept terms">');
	const inventory = FormInventory.Extract();

	assert.strictEqual(inventory[0].type, 'checkbox');
	assert.strictEqual(inventory[0].label, 'Accept terms');
	assert.deepStrictEqual(jsonOf(inventory[0].options.map(o => o.value)), ['true', 'false']);
});

test('field key prefers name, else normalized label with trailing colons stripped', () => {
	const { FormInventory } = fresh(`
		<label for="e">Your Email *:</label>
		<input id="e" type="email" />
	`);
	const inventory = FormInventory.Extract();

	assert.strictEqual(inventory[0].name, '');
	assert.strictEqual(inventory[0].label, 'Your Email *:');
	assert.strictEqual(inventory[0].fieldKey, 'your email');
});

test('wrapping labels and placeholders resolve when no label element exists', () => {
	const { FormInventory } = fresh(`
		<label>Nickname <input type="text" name="nick"></label>
		<input type="tel" name="phone" placeholder="Phone number">
	`);
	const inventory = FormInventory.Extract();

	assert.strictEqual(inventory[0].label, 'Nickname');
	assert.strictEqual(inventory[1].label, 'Phone number');
});

test('registry maps every field id to its element for the fill handler', () => {
	const { env, FormInventory } = fresh('<input type="text" name="a"><input type="text" name="b">');
	FormInventory.Extract();

	assert.ok(env.grab('FormInventory.Registry.get("f1").element'));
	assert.ok(env.grab('FormInventory.Registry.get("f2").element'));
});
