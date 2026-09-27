import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv } from './helpers/env.js';

const MARKUP = `
<div id="Pick" class="dropdown">
	<button type="button" class="form-select text-start">auto</button>
	<div class="dropdown-menu w-100" style="max-height:240px;overflow-y:auto">
		<button type="button" class="dropdown-item" data-value="auto">auto</button>
		<button type="button" class="dropdown-item" data-value="job_detail">job_detail</button>
		<button type="button" class="dropdown-item" data-value="apply_form">apply_form</button>
	</div>
</div>
<div id="Outside">elsewhere</div>`;

function fresh() {
	const env = createEnv({ dom: true, url: 'chrome-extension://panel/index.html' });
	env.sandbox.document.body.innerHTML = MARKUP;
	env.load('application/dropdown.js');
	env.grab('Dropdown').Attach(env.sandbox.document.getElementById('Pick'));
	return env;
}

const toggleOf = env => env.sandbox.document.querySelector('#Pick .form-select');
const menuOf = env => env.sandbox.document.querySelector('#Pick .dropdown-menu');
const itemsOf = env => Array.from(env.sandbox.document.querySelectorAll('#Pick .dropdown-item'));

test('attach reads the static items and starts on the first value', () => {
	const env = fresh();
	const el = env.sandbox.document.getElementById('Pick');

	assert.strictEqual(el.value, 'auto');
	assert.deepStrictEqual(Array.from(el.options).map(o => o.value), ['auto', 'job_detail', 'apply_form']);
	assert.deepStrictEqual(Array.from(el.options).map(o => o.text), ['auto', 'job_detail', 'apply_form']);
	assert.ok(itemsOf(env)[0].classList.contains('active'));
	assert.strictEqual(toggleOf(env).textContent, 'auto');
});

test('the toggle opens and closes the menu', () => {
	const env = fresh();

	toggleOf(env).click();
	assert.ok(menuOf(env).classList.contains('show'));

	toggleOf(env).click();
	assert.ok(!menuOf(env).classList.contains('show'));
});

test('an item click sets the value, fires change once and closes', () => {
	const env = fresh();
	const el = env.sandbox.document.getElementById('Pick');
	const seen = [];
	el.addEventListener('change', () => seen.push(el.value));

	toggleOf(env).click();
	itemsOf(env)[1].click();

	assert.strictEqual(el.value, 'job_detail');
	assert.deepStrictEqual(seen, ['job_detail']);
	assert.ok(!menuOf(env).classList.contains('show'));
	assert.strictEqual(toggleOf(env).textContent, 'job_detail');
	assert.ok(itemsOf(env)[1].classList.contains('active'));
	assert.ok(!itemsOf(env)[0].classList.contains('active'));
});

test('a click outside closes the open menu', () => {
	const env = fresh();

	toggleOf(env).click();
	assert.ok(menuOf(env).classList.contains('show'));

	env.sandbox.document.getElementById('Outside').click();
	assert.ok(!menuOf(env).classList.contains('show'));
});

test('Escape closes the open menu', () => {
	const env = fresh();

	toggleOf(env).click();
	assert.ok(menuOf(env).classList.contains('show'));

	env.sandbox.document.dispatchEvent(new env.sandbox.window.KeyboardEvent('keydown', { key: 'Escape' }));
	assert.ok(!menuOf(env).classList.contains('show'));
});

test('the options setter rebuilds the items and resets the value to the first', () => {
	const env = fresh();
	const el = env.sandbox.document.getElementById('Pick');

	el.options = [
		{ value: 'Ranking', text: 'ranking' },
		{ value: 'Resume', text: 'resume' },
	];

	assert.deepStrictEqual(Array.from(el.options).map(o => o.value), ['Ranking', 'Resume']);
	assert.strictEqual(el.value, 'Ranking');
	assert.strictEqual(toggleOf(env).textContent, 'ranking');
	assert.ok(itemsOf(env)[0].classList.contains('active'));

	el.value = 'Resume';
	assert.strictEqual(el.value, 'Resume');
	assert.strictEqual(toggleOf(env).textContent, 'resume');
	assert.ok(itemsOf(env)[1].classList.contains('active'));
	assert.ok(!itemsOf(env)[0].classList.contains('active'));
});

test('setting value programmatically does not fire change', () => {
	const env = fresh();
	const el = env.sandbox.document.getElementById('Pick');
	let fired = 0;
	el.addEventListener('change', () => fired++);

	el.value = 'apply_form';

	assert.strictEqual(fired, 0);
	assert.strictEqual(el.value, 'apply_form');
	assert.strictEqual(toggleOf(env).textContent, 'apply_form');
});
