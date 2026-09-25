(function () {
    const KEY = 'theme';
    const ORDER = ['light', 'dark', 'system'];
    const ICONS = { light: 'fa-sun', dark: 'fa-moon', system: 'fa-desktop' };
    const LABELS = { light: 'Light', dark: 'Dark', system: 'System' };
    const media = window.matchMedia('(prefers-color-scheme: dark)');

    function current() {
        const stored = localStorage.getItem(KEY);
        return ORDER.indexOf(stored) >= 0 ? stored : 'system';
    }

    function apply(pref) {
        const dark = pref === 'dark' || (pref === 'system' && media.matches);
        document.documentElement.setAttribute('data-bs-theme', dark ? 'dark' : 'light');

        const icon = document.getElementById('theme-toggle-icon');
        if (icon) icon.className = 'fa-solid ' + ICONS[pref];

        const button = document.getElementById('theme-toggle');
        if (button) {
            const label = 'Theme: ' + LABELS[pref];
            button.title = label;
            button.setAttribute('aria-label', label);
        }
    }

    media.addEventListener('change', function () {
        if (current() === 'system') apply('system');
    });

    const button = document.getElementById('theme-toggle');
    if (button) {
        button.addEventListener('click', function () {
            const next = ORDER[(ORDER.indexOf(current()) + 1) % ORDER.length];
            localStorage.setItem(KEY, next);
            apply(next);
        });
    }

    apply(current());
})();
