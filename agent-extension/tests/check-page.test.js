import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv, jsonOf } from './helpers/env.js';

function fresh(options = {}) {
	const env = createEnv({
		dom: true,
		url: options.url ?? 'https://www.linkedin.com/jobs/123',
	});
	delete options.url;
	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/action-handler.js');
	env.load('controllers/challenge-detector.js');
	env.load('controllers/check-page.js');
	return {
		env,
		OnDashboard: () => env.grab('OnDashboard')(),
		SendingPageInfo: scope => env.grab('SendingPageInfo')(scope),
		RetryableError: result => env.grab('RetryableError')(result),
		startHeartbeat: () => env.grab('StartHeartbeat')(),
		onPageLoad: () => env.grab('ActionHandler.OnPageLoad')(),
	};
}

const sentTitles = env => jsonOf(env.chrome.runtime.sent.map(m => m.title));

test('OnDashboard is true when the page origin matches the stored server URL', async () => {
	const { env, OnDashboard } = fresh({ url: 'http://localhost:8081/dashboard' });
	env.chrome.storage.local.set({ SERVER_URL: 'http://localhost:8081/' });
	assert.strictEqual(await OnDashboard(), true);
});

test('OnDashboard falls back to the trend-list marker when origins differ', async () => {
	const { env, OnDashboard } = fresh({ url: 'https://random.example/' });
	env.chrome.storage.local.set({ SERVER_URL: 'http://localhost:8081/' });
	assert.strictEqual(await OnDashboard(), false);
	env.sandbox.document.body.innerHTML = '<div id="job-seeker-trend-list"></div>';
	assert.strictEqual(await OnDashboard(), true);
});

test('OnDashboard swallows storage errors and falls through to the marker check', async () => {
	const { env, OnDashboard } = fresh({ url: 'https://random.example/' });
	env.chrome.storage.local.errors.get = 'boom';
	assert.strictEqual(await OnDashboard(), false);
	env.sandbox.document.body.innerHTML = '<div id="job-seeker-trend-list"></div>';
	assert.strictEqual(await OnDashboard(), true);
	assert.ok(env.console.entries.some(e => e.level === 'error' && e.args.includes('OnDashboard')));
});

test('OnPageLoad skips everything on the dashboard', async () => {
	const { env, onPageLoad } = fresh({ url: 'http://localhost:8081/dashboard' });
	env.chrome.storage.local.set({ SERVER_URL: 'http://localhost:8081/' });
	onPageLoad();
	await env.advance(1000);
	assert.strictEqual(env.chrome.runtime.sent.length, 0);
	assert.strictEqual(env.clock.pending().length, 0);
});

test('OnPageLoad stops when the scopes call fails', async () => {
	const { env, onPageLoad } = fresh();
	env.chrome.runtime.respondWith(message =>
		message.title === 'scopes' ? { error: 'http', status: 500 } : { ok: true });
	onPageLoad();
	await env.advance(1000);
	assert.deepStrictEqual(sentTitles(env), ['scopes']);
	assert.ok(env.console.entries.some(e => e.level === 'error' && e.args.includes('scopes failed')));
	assert.ok(!env.clock.pending().some(t => t.interval === 0));
});

test('OnPageLoad sends the page for a case-insensitive scope match and starts the heartbeat', async () => {
	const { env, onPageLoad } = fresh({ url: 'https://www.linkedin.com/jobs/123' });
	env.chrome.runtime.respondWith(message => {
		if (message.title === 'scopes') return [{ name: 'LinkedIn', domain: 'LinkedIn\\.com' }];
		if (message.title === 'send') {
			return { commands: [{ action: 'reload' }], close_timeout_ms: 1500 };
		}
		return { ok: true };
	});
	onPageLoad();
	await env.advance(1000);
	assert.deepStrictEqual(sentTitles(env), ['scopes', 'send']);
	const send = env.chrome.runtime.sent[1];
	assert.strictEqual(send.params.agency, 'LinkedIn');
	assert.strictEqual(send.params.url, 'https://www.linkedin.com/jobs/123');
	assert.ok(send.params.content.includes('<body'));
	assert.strictEqual(env.sandbox.location.reloadCalls, 1);
	assert.ok(env.clock.pending().some(t => t.interval === 30000));
	assert.ok(env.clock.pending().some(t => t.due === 1500));
});

test('OnPageLoad honors scope.waiting with jitter before sending', async () => {
	const { env, onPageLoad } = fresh();
	env.chrome.runtime.respondWith(message => {
		if (message.title === 'scopes') {
			return [{ name: 'A', domain: 'linkedin\\.com', waiting: 2500 }];
		}
		return { commands: [], close_timeout_ms: 5 };
	});
	onPageLoad();
	await env.advance(1000);
	assert.deepStrictEqual(sentTitles(env), ['scopes']);
	await env.advance(1874);
	assert.strictEqual(env.chrome.runtime.sent.length, 1);
	await env.advance(3126 - 1874 + 1);
	assert.strictEqual(env.chrome.runtime.sent.length, 2);
});

test('OnPageLoad does nothing for a non-matching hostname', async () => {
	const { env, onPageLoad } = fresh({ url: 'https://www.example.com/x' });
	env.chrome.runtime.respondWith(message =>
		message.title === 'scopes' ? [{ name: 'Indeed', domain: 'indeed\\.com' }] : { commands: [] });
	onPageLoad();
	await env.advance(1000);
	assert.deepStrictEqual(sentTitles(env), ['scopes']);
	assert.ok(!env.clock.pending().some(t => t.interval === 30000));
	assert.ok(!env.clock.pending().some(t => t.interval === 0));
});

test('a matching hostname arms the default close timer', async () => {
	const { env, onPageLoad } = fresh();
	env.chrome.runtime.respondWith(message => {
		if (message.title === 'scopes') return [{ name: 'LinkedIn', domain: 'linkedin\\.com' }];
		return { commands: [] };
	});
	onPageLoad();
	await env.advance(1000);
	assert.ok(env.clock.pending().some(t => t.interval === 0 && t.due > 80000));
});

test('SendingPageInfo retries retryable failures with 5s/10s backoff and stops on success', async () => {
	const { env, SendingPageInfo } = fresh();
	let sends = 0;
	env.chrome.runtime.respondWith(message => {
		if (message.title !== 'send') return { ok: true };
		sends++;
		if (sends < 3) return { error: 'network', status: 0 };
		return { commands: [{ action: 'reload' }], close_timeout_ms: 700 };
	});
	const promise = SendingPageInfo({ name: 'A', domain: 'linkedin\\.com' });
	await env.flush();
	assert.strictEqual(sends, 1);
	await env.advance(4999);
	assert.strictEqual(sends, 1);
	await env.advance(1);
	assert.strictEqual(sends, 2);
	await env.advance(9999);
	assert.strictEqual(sends, 2);
	await env.advance(1);
	assert.strictEqual(sends, 3);
	await promise;
	assert.strictEqual(env.sandbox.location.reloadCalls, 1);
	assert.ok(env.console.entries.some(e => e.level === 'error' && e.args.includes('send failed')));
});

test('SendingPageInfo retries http errors with status >= 500 up to three attempts', async () => {
	const { env, SendingPageInfo } = fresh();
	let sends = 0;
	env.chrome.runtime.respondWith(message => {
		if (message.title !== 'send') return { ok: true };
		sends++;
		return { error: 'http', status: 503 };
	});
	const promise = SendingPageInfo({ name: 'A', domain: 'linkedin\\.com' });
	await env.flush();
	assert.strictEqual(sends, 1);
	await env.advance(5000);
	assert.strictEqual(sends, 2);
	await env.advance(10000);
	assert.strictEqual(sends, 3);
	await promise;
});

test('SendingPageInfo does not retry a 4xx http error', async () => {
	const { env, SendingPageInfo } = fresh();
	let sends = 0;
	env.chrome.runtime.respondWith(message => {
		if (message.title !== 'send') return { ok: true };
		sends++;
		return { error: 'http', status: 404 };
	});
	await SendingPageInfo({ name: 'A', domain: 'linkedin\\.com' });
	assert.strictEqual(sends, 1);
});

test('RetryableError classifies transport and 5xx errors as retryable', () => {
	const { RetryableError } = fresh();
	for (const error of ['network', 'timeout', 'no-response']) {
		assert.strictEqual(RetryableError({ error, status: 0 }), true, error);
	}
	assert.strictEqual(RetryableError({ error: 'http', status: 500 }), true);
	assert.strictEqual(RetryableError({ error: 'http', status: 503 }), true);
	assert.strictEqual(RetryableError({ error: 'http', status: 404 }), false);
	assert.strictEqual(RetryableError({ error: 'client', status: 0 }), false);
	assert.strictEqual(RetryableError({ error: 'invalid-json', status: 200 }), false);
});

test('the heartbeat only beats while the document is visible', async () => {
	const { env, startHeartbeat } = fresh();
	env.chrome.runtime.respondWith(() => ({ ok: true }));
	startHeartbeat();
	await env.advance(30000);
	assert.deepStrictEqual(sentTitles(env), ['heartbeat']);
	await env.advance(30000);
	assert.strictEqual(env.chrome.runtime.sent.length, 2);
	Object.defineProperty(env.sandbox.document, 'visibilityState', {
		value: 'hidden',
		configurable: true,
	});
	await env.advance(60000);
	assert.strictEqual(env.chrome.runtime.sent.length, 2);
});

test('StartHeartbeat is idempotent', async () => {
	const { env, startHeartbeat } = fresh();
	env.chrome.runtime.respondWith(message =>
		message.title === 'heartbeat' ? { ok: true } : { ok: true });
	startHeartbeat();
	startHeartbeat();
	await env.advance(90000);
	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'heartbeat').length, 3);
});

test('a failed heartbeat logs a warning', async () => {
	const { env, startHeartbeat } = fresh();
	env.chrome.runtime.respondWith(message =>
		message.title === 'heartbeat' ? { error: 'no-response', status: 0 } : { ok: true });
	startHeartbeat();
	await env.advance(30000);
	assert.ok(env.console.entries.some(
		e => e.level === 'warn' && e.args.includes('heartbeat failed')
	));
});

const challengeResponder = () => {
	return message => {
		if (message.title === 'scopes') return [{ name: 'A', domain: 'linkedin\\.com' }];
		if (message.title === 'send')
			return message.params.challenge
				? { commands: [], close_timeout_ms: 86400000 }
				: { commands: [{ action: 'reload' }], close_timeout_ms: 700 };
		return { ok: true };
	};
};

test('a challenge page sends challenge:true, arms the long close timer and holds', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.body.innerHTML = '<div id="challenge-form"></div>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);

	assert.deepStrictEqual(sentTitles(env), ['scopes', 'send']);
	assert.strictEqual(env.chrome.runtime.sent[1].params.challenge, true);
	assert.strictEqual(env.chrome.runtime.sent[1].params.challenge_kind, 'cf-interstitial');
	assert.ok(env.clock.pending().some(t => t.due === 86400000));
	assert.ok(env.clock.pending().some(t => t.interval === 30000));
	assert.ok(env.clock.pending().some(t => t.interval === 5000));
	assert.ok(env.console.entries.some(e => e.level === 'warn' && e.args.includes('challenge hold')));
});

test('a 403-titled page sends the http-403 challenge kind and holds', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.title = '403 Forbidden';
	env.sandbox.document.body.innerHTML = '<center><h1>403 Forbidden</h1></center>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);

	assert.deepStrictEqual(sentTitles(env), ['scopes', 'send']);
	assert.strictEqual(env.chrome.runtime.sent[1].params.challenge, true);
	assert.strictEqual(env.chrome.runtime.sent[1].params.challenge_kind, 'http-403');
	assert.ok(env.clock.pending().some(t => t.due === 86400000));
	assert.ok(env.clock.pending().some(t => t.interval === 5000));
	assert.ok(env.console.entries.some(e => e.level === 'warn' && e.args.includes('challenge hold')));
});

test('a clean page is sent without the challenge flag', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.body.innerHTML = '<div class="jobs-list"></div>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);

	assert.strictEqual(env.chrome.runtime.sent[1].params.challenge, undefined);
	assert.ok(env.clock.pending().some(t => t.due === 700));
	assert.ok(!env.clock.pending().some(t => t.interval === 5000));
});

test('the heartbeat keeps beating on a hidden tab while the challenge holds', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.body.innerHTML = '<div id="challenge-form"></div>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);

	Object.defineProperty(env.sandbox.document, 'visibilityState', {
		value: 'hidden',
		configurable: true,
	});
	await env.advance(30000);

	assert.ok(sentTitles(env).includes('heartbeat'));
});

test('the watcher resumes the flow once the challenge box disappears, after the cooldown', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.body.innerHTML = '<div id="challenge-form"></div>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);
	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'send').length, 1);

	env.sandbox.document.body.innerHTML = '<div class="jobs-list"></div>';
	await env.advance(5000);
	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'send').length, 1);

	await env.advance(39999);
	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'send').length, 1);
	await env.advance(1);

	const sends = env.chrome.runtime.sent.filter(m => m.title === 'send');
	assert.strictEqual(sends.length, 2);
	assert.strictEqual(sends[1].params.challenge, undefined);
	assert.strictEqual(env.sandbox.location.reloadCalls, 1);
	assert.strictEqual(env.grab('challenge_hold'), false);
	assert.ok(!env.clock.pending().some(t => t.interval === 5000));
});

test('a fresh page load right after a challenge waits out the cooldown before sending', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.body.innerHTML = '<div id="challenge-form"></div>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);
	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'send').length, 1);

	env.sandbox.document.body.innerHTML = '<div class="jobs-list"></div>';
	onPageLoad();
	await env.advance(1000);
	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'send').length, 1);

	await env.advance(43999);
	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'send').length, 1);
	await env.advance(1);

	const sends = env.chrome.runtime.sent.filter(m => m.title === 'send');
	assert.strictEqual(sends.length, 2);
	assert.strictEqual(sends[1].params.challenge, undefined);
});

test('the watcher keeps waiting while the challenge box stays', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.body.innerHTML = '<div id="challenge-form"></div>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);
	await env.advance(15000);

	assert.strictEqual(env.chrome.runtime.sent.filter(m => m.title === 'send').length, 1);
	assert.strictEqual(env.grab('challenge_hold'), true);
});

test('OnPageLoad clears an existing challenge hold before running again', async () => {
	const { env, onPageLoad } = fresh();
	env.sandbox.document.body.innerHTML = '<div id="challenge-form"></div>';
	env.chrome.runtime.respondWith(challengeResponder());

	onPageLoad();
	await env.advance(1000);
	assert.strictEqual(env.grab('challenge_hold'), true);

	onPageLoad();
	assert.strictEqual(env.grab('challenge_hold'), false);
	assert.ok(!env.clock.pending().some(t => t.interval === 5000));

	await env.advance(1000);
	assert.strictEqual(env.grab('challenge_hold'), true);
	assert.ok(env.clock.pending().some(t => t.interval === 5000));
});

test('the load event triggers OnPageLoad', async () => {
	const env = createEnv({ dom: true, url: 'https://www.example.com/', autoLoadGate: false });
	await env.sandbox.happyDOM.waitUntilComplete();
	env.load('controllers/storage-handler.js');
	env.load('controllers/background-messaging.js');
	env.load('controllers/action-handler.js');
	env.load('controllers/challenge-detector.js');
	env.load('controllers/check-page.js');
	env.chrome.runtime.respondWith(message =>
		message.title === 'scopes' ? [] : { commands: [] });
	env.sandbox.dispatchEvent(new env.sandbox.Event('load'));
	await env.advance(1000);
	assert.deepStrictEqual(sentTitles(env), ['scopes']);
});
