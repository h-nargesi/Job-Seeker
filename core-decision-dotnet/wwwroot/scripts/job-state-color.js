function job_state_color(key) {
    if (!key) return '#6c757d';

    const css = key.replace(/[A-Z]/g, match => '-' + match.toLowerCase());
    const value = getComputedStyle(document.documentElement)
        .getPropertyValue('--job-state-' + css)
        .trim();

    return value || '#6c757d';
}
