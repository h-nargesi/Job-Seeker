import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh(url = 'https://www.example.com/page') {
	const env = createEnv({ dom: true, url });
	env.chrome.runtime.respondWith(() => ({ ok: true }));
	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/action-handler.js');
	return { env, ActionHandler: env.grab('ActionHandler') };
}

const sentTitles = env => env.chrome.runtime.sent.map(m => m.title);

test('Handle with null commands returns early', async () => {
	const { env, ActionHandler } = fresh();
	await ActionHandler.Handle(null);
	assert.ok(env.console.entries.some(e => e.level === 'error' && e.args.includes('no commands')));
	assert.strictEqual(env.chrome.runtime.sent.length, 0);
});

test('Handle skips falsy entries and counts the executed ones', async () => {
	const { env, ActionHandler } = fresh();
	const commands = [
		{ action: 'wait', params: { miliseconds: 50 } },
		null,
		{ action: 'wait', params: { miliseconds: 50 } },
	];
	const promise = ActionHandler.Handle(commands);
	await env.advance(150);
	await env.advance(100);
	await promise;
	assert.ok(env.console.entries.some(e => e.args.includes('Action Count') && e.args.includes(2)));
});

test('go assigns the URL to window.location', async () => {
	const { env, ActionHandler } = fresh();
	await ActionHandler.Execute({ action: 'go', params: { url: 'https://next.example/x' } });
	assert.strictEqual(env.sandbox.location, 'https://next.example/x');
});

test('open delegates to BackgroundMessaging.OpenTab', async () => {
	const { env, ActionHandler } = fresh();
	await ActionHandler.Execute({ action: 'open', params: { url: 'https://tab.example/' } });
	const last = env.chrome.runtime.sent.at(-1);
	assert.deepStrictEqual(jsonOf(last), {
		title: 'open-tab',
		params: { url: 'https://tab.example/' },
		id: 1,
	});
});

test('fill sets .value on form elements and innerText elsewhere', async () => {
	const { env, ActionHandler } = fresh();
	env.sandbox.document.body.innerHTML =
		'<input id="a"></input><textarea id="b"></textarea><div id="c"></div>';
	const promise = ActionHandler.Execute({
		action: 'fill',
		object: '#a, #b, #c',
		params: { value: 'V' },
	});
	await env.advance(400);
	await promise;
	const doc = env.sandbox.document;
	assert.strictEqual(doc.getElementById('a').value, 'V');
	assert.strictEqual(doc.getElementById('b').value, 'V');
	assert.strictEqual(doc.getElementById('c').innerText, 'V');
});

test('click fires a click event on every matched element', async () => {
	const { env, ActionHandler } = fresh();
	let clicks = 0;
	const btn = env.sandbox.document.createElement('button');
	btn.id = 'btn';
	btn.addEventListener('click', () => clicks++);
	env.sandbox.document.body.appendChild(btn);
	const promise = ActionHandler.Execute({ action: 'click', object: '#btn' });
	await env.advance(400);
	await promise;
	assert.strictEqual(clicks, 1);
});

test('close sends close-tab', async () => {
	const { env, ActionHandler } = fresh();
	await ActionHandler.Execute({ action: 'close' });
	assert.ok(sentTitles(env).includes('close-tab'));
});

test('close with dontclose sends nothing', async () => {
	const { env, ActionHandler } = fresh();
	await ActionHandler.Execute({ action: 'close' }, true);
	assert.strictEqual(env.chrome.runtime.sent.length, 0);
});

test('reload calls location.reload', async () => {
	const { env, ActionHandler } = fresh();
	await ActionHandler.Execute({ action: 'reload' });
	assert.strictEqual(env.sandbox.location.reloadCalls, 1);
});

test('wait resolves after the requested milliseconds', async () => {
	const { env, ActionHandler } = fresh();
	let done = false;
	const promise = ActionHandler
		.Execute({ action: 'wait', params: { miliseconds: 250 } })
		.then(() => { done = true; });
	await env.advance(249);
	assert.strictEqual(done, false);
	await env.advance(1);
	await promise;
	assert.strictEqual(done, true);
});

test('an unknown action logs an error (with the source typo) and does not throw', async () => {
	const { env, ActionHandler } = fresh();
	await ActionHandler.Execute({ action: 'bogus' });
	assert.ok(env.console.entries.some(e => e.level === 'error' && e.args.includes('Unkown action')));
});

test('fill on an empty selector does nothing and never warns (current behavior)', async () => {
	const { env, ActionHandler } = fresh();
	const promise = ActionHandler.Execute({
		action: 'fill',
		object: '#missing',
		params: { value: 'V' },
	});
	await env.advance(400);
	await promise;
	assert.ok(!env.console.entries.some(e => e.args.includes('Not found')));
});

test('click on an empty selector does nothing and never warns (current behavior)', async () => {
	const { env, ActionHandler } = fresh();
	const promise = ActionHandler.Execute({ action: 'click', object: '#missing' });
	await env.advance(400);
	await promise;
	assert.ok(!env.console.entries.some(e => e.args.includes('Not found')));
});

test('recheck invokes OnPageLoad when it is set', async () => {
	const { env, ActionHandler } = fresh();
	let calls = 0;
	ActionHandler.OnPageLoad = () => calls++;
	await ActionHandler.Execute({ action: 'recheck' });
	assert.strictEqual(calls, 1);
});

test('recheck with OnPageLoad unset warns and then throws a TypeError (current behavior)', async () => {
	const { env, ActionHandler } = fresh();
	await assert.rejects(
		ActionHandler.Execute({ action: 'recheck' }),
		error => error.name === 'TypeError' && error.message.includes('OnPageLoad')
	);
	assert.ok(env.console.entries.some(
		e => e.level === 'warn' && e.args.includes('The OnPageLoad event is not set!')
	));
});

test('SetCloseTimer fires CloseTab after the default 90s', async () => {
	const { env, ActionHandler } = fresh();
	ActionHandler.SetCloseTimer();
	await env.advance(89999);
	assert.ok(!sentTitles(env).includes('close-tab'));
	await env.advance(1);
	assert.ok(sentTitles(env).includes('close-tab'));
});

test('a second SetCloseTimer cancels the first', async () => {
	const { env, ActionHandler } = fresh();
	ActionHandler.SetCloseTimer();
	ActionHandler.SetCloseTimer(5000);
	await env.advance(4999);
	assert.strictEqual(sentTitles(env).filter(t => t === 'close-tab').length, 0);
	await env.advance(1);
	assert.strictEqual(sentTitles(env).filter(t => t === 'close-tab').length, 1);
	await env.advance(90000);
	assert.strictEqual(sentTitles(env).filter(t => t === 'close-tab').length, 1);
});

test('SetCloseTimer honors a custom delay', async () => {
	const { env, ActionHandler } = fresh();
	ActionHandler.SetCloseTimer(1234);
	await env.advance(1233);
	assert.strictEqual(sentTitles(env).filter(t => t === 'close-tab').length, 0);
	await env.advance(1);
	assert.strictEqual(sentTitles(env).filter(t => t === 'close-tab').length, 1);
});
