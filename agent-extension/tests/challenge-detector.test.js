import test from 'node:test';
import assert from 'node:assert/strict';
import { createEnv } from './helpers/env.js';

function detect(html, title) {
	const env = createEnv({ dom: true });
	if (title !== undefined) env.sandbox.document.title = title;
	env.sandbox.document.body.innerHTML = html;
	env.load('controllers/challenge-detector.js');
	return env.grab('ChallengeDetector.Detect')(env.sandbox.document);
}

test('detects cloudflare interstitial markers', () => {
	assert.strictEqual(detect('<div id="challenge-form"></div>'), 'cf-interstitial');
	assert.strictEqual(detect('<div id="challenge-running"></div>'), 'cf-interstitial');
	assert.strictEqual(detect('<div id="challenge-stage"></div>'), 'cf-interstitial');
	assert.strictEqual(detect('<div id="challenge-error-text">x</div>'), 'cf-interstitial');
	assert.strictEqual(detect('<div id="cf-please-wait"></div>'), 'cf-interstitial');
	assert.strictEqual(detect('<script src="https://x.test/cdn-cgi/challenge-platform/h/b/or.js"></script>'), 'cf-interstitial');
	assert.strictEqual(detect('<iframe src="https://x.test/cdn-cgi/challenge-platform/g/cv/123"></iframe>'), 'cf-interstitial');
});

test('detects turnstile widgets and frames but not the invisible variant', () => {
	assert.strictEqual(detect('<div class="cf-turnstile" data-sitekey="k"></div>'), 'cf-turnstile');
	assert.strictEqual(detect('<iframe src="https://challenges.cloudflare.com/turnstile/v0/xy"></iframe>'), 'cf-turnstile');
	assert.strictEqual(detect('<iframe src="https://challenges.cloudflare.com/cdn-cgi/challenge-platform/xy"></iframe>'), 'cf-interstitial');
	assert.strictEqual(detect('<div class="cf-turnstile" data-size="invisible"></div>'), null);
});

test('detects visible recaptcha and hcaptcha widgets but not invisible variants', () => {
	assert.strictEqual(detect('<div class="g-recaptcha"></div>'), 'recaptcha');
	assert.strictEqual(detect('<div class="g-recaptcha" data-size="invisible"></div>'), null);
	assert.strictEqual(detect('<div class="h-captcha" data-sitekey="k"></div>'), 'hcaptcha');
	assert.strictEqual(detect('<div class="h-captcha" data-size="invisible"></div>'), null);
});

test('detects captcha frames by title', () => {
	assert.strictEqual(detect('<iframe title="recaptcha challenge expires after two minutes"></iframe>'), 'captcha');
	assert.strictEqual(detect('<iframe title="Captcha"></iframe>'), 'captcha');
});

test('does not fire on ordinary pages', () => {
	assert.strictEqual(detect('<div class="jobs-list"><a href="#">Senior .NET Developer</a></div>'), null);
	assert.strictEqual(detect('<form id="login"><input name="user"><input name="pass" type="password"></form>'), null);
	assert.strictEqual(detect('<textarea id="cover-letter"></textarea>'), null);
});

test('detects rendered 403 error pages by title', () => {
	assert.strictEqual(detect('<div></div>', '403 Forbidden'), 'http-403');
	assert.strictEqual(detect('<div></div>', 'Access Denied'), 'http-403');
	assert.strictEqual(detect('<div></div>', 'Zugriff verweigert'), 'http-403');
	assert.strictEqual(detect('<div></div>', 'Example.com | 403'), 'http-403');
});

test('detects nginx-style 403 pages via h1 and a tiny body', () => {
	assert.strictEqual(detect('<center><h1>403 Forbidden</h1></center><hr><center>nginx</center>'), 'http-403');
	assert.strictEqual(detect('<h1>Access Denied</h1><p>You do not have permission.</p>'), 'http-403');
});

test('does not fire when 403 appears only in body or job content', () => {
	assert.strictEqual(detect('<div class="jobs-list"><a href="#">Job reference 403: .NET Developer</a></div>', 'Jobs'), null);
	assert.strictEqual(
		detect('<h1>Senior .NET Developer</h1><p>Error code 403 appeared in the build logs yesterday.</p>', 'Senior .NET Developer'),
		null);
});

test('does not fire on an h1 403 marker with a full-size body', () => {
	assert.strictEqual(
		detect(`<h1>403 Forbidden</h1><div class="jobs-list">${'<a href="#">Senior .NET Developer vacancy</a>'.repeat(30)}</div>`, 'Jobs'),
		null);
});

test('selector kinds keep priority over the http-403 text markers', () => {
	assert.strictEqual(detect('<div id="challenge-form"></div>', '403 Forbidden'), 'cf-interstitial');
});
