import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { EXTENSION_ROOT } from './helpers/env.js';

const manifest = JSON.parse(fs.readFileSync(path.join(EXTENSION_ROOT, 'manifest.json'), 'utf8'));

test('manifest declares the side panel and the settings-only popup', () => {
	assert.ok(manifest.permissions.includes('sidePanel'));
	assert.strictEqual(manifest.side_panel?.default_path, 'application/panel.html');
	assert.strictEqual(manifest.action?.default_popup, 'application/settings.html');

	assert.ok(fs.existsSync(path.join(EXTENSION_ROOT, manifest.side_panel.default_path)));
	assert.ok(fs.existsSync(path.join(EXTENSION_ROOT, manifest.action.default_popup)));
});

test('extension pages load remote CSS but never remote or bundled bootstrap JS', () => {
	for (const page of ['application/panel.html', 'application/settings.html']) {
		const html = fs.readFileSync(path.join(EXTENSION_ROOT, page), 'utf8');

		assert.ok(html.includes('cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css'), page);
		assert.ok(!/bootstrap[^"]*\.js/.test(html), page);
		assert.ok(!html.includes('data-bs-'), page);
	}
});
