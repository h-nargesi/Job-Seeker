function element_value(id) {
    const element = document.getElementById(id);
    return element ? element.value : '';
}

function show_status(prefix, id, message, ok) {
    const element = document.getElementById(prefix + '-status-' + id);
    if (!element) return;
    element.textContent = message;
    element.className = 'small ' + (ok ? 'text-body-secondary' : 'text-danger');
}

async function post_json(url, body) {
    try {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        const text = await response.text();
        return { ok: response.ok, text: text.length > 0 ? text : (response.ok ? 'Saved' : 'Failed') };
    } catch (e) {
        return { ok: false, text: e.message };
    }
}

async function post_empty(url) {
    try {
        const response = await fetch(url, { method: 'POST' });
        if (response.ok) return null;
        return (await response.text()) || 'Failed';
    } catch (e) {
        return e.message;
    }
}

async function save_setting(key) {
    const element = document.getElementById('setting-' + key);
    if (!element) return;

    const result = await post_json('/settings/appsetting', { key: key, value: element.value });
    show_status('setting', key, result.ok ? 'Saved' : result.text, result.ok);
}

function option_row(id) {
    return {
        jobOptionID: id === 'new' ? 0 : id,
        efective: id !== 'new' && document.getElementById('option-row-' + id)?.dataset.efective === 'true',
        category: element_value('option-' + id + '-category'),
        score: Number.parseInt(element_value('option-' + id + '-score'), 10),
        title: element_value('option-' + id + '-title'),
        pattern: element_value('option-' + id + '-pattern'),
        settings: element_value('option-' + id + '-settings')
    };
}

async function save_option(id) {
    const row = option_row(id);

    if (!Number.isFinite(row.score)) {
        show_status('option', id, 'Score must be an integer', false);
        return;
    }
    if (!row.title || !row.pattern) {
        show_status('option', id, 'Title and pattern are required', false);
        return;
    }

    const result = await post_json('/settings/optionsave', row);
    if (result.ok) location.reload();
    else show_status('option', id, result.text, false);
}

async function toggle_option(id, effective) {
    const error = await post_empty(`/settings/optiontoggle?joboptionid=${id}&effective=${effective}`);
    if (!error) location.reload();
    else show_status('option', id, error, false);
}

async function delete_option(id) {
    if (!confirm('Delete this option?')) return;

    const error = await post_empty('/settings/optiondelete?joboptionid=' + id);
    if (!error) location.reload();
    else show_status('option', id, error, false);
}

async function save_waiting(name) {
    const element = document.getElementById('waiting-' + name);
    if (!element) return;

    const raw = element.value.trim();
    let waiting = null;
    if (raw.length > 0) {
        waiting = Number.parseInt(raw, 10);
        if (!Number.isFinite(waiting)) {
            show_status('waiting', name, 'Waiting must be an integer (ms)', false);
            return;
        }
    }

    const result = await post_json('/settings/agencywaiting', { agency: name, waiting: waiting });
    if (result.ok) location.reload();
    else show_status('waiting', name, result.text, false);
}

async function reload_settings() {
    const error = await post_empty('/settings/reload');
    if (!error) show_status('setting', 'floor', 'Settings reloaded', true);
    else show_status('setting', 'floor', error, false);
}
