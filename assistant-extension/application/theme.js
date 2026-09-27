console.log("ASSISTANT", "theme.js");

class ThemeHandler {

    static MODES = ["system", "light", "dark"];
    static QUERY = "(prefers-color-scheme: dark)";
    static selects = [];
    static mode = null;

    static Start() {
        window.matchMedia(ThemeHandler.QUERY).addEventListener("change", function () {
            if (ThemeHandler.mode === "system") ThemeHandler.Apply("system");
        });

        chrome.storage.onChanged.addListener(function (changes, area) {
            if (area === "local" && changes[StorageHandler.THEME])
                ThemeHandler.Apply(changes[StorageHandler.THEME].newValue);
        });

        return ThemeHandler.Load();
    }

    static async Load() {
        ThemeHandler.Apply(await StorageHandler.ThemeAsync());
    }

    static async Set(mode) {
        const chosen = ThemeHandler.MODES.includes(mode) ? mode : "system";
        StorageHandler.Theme = chosen;
        ThemeHandler.Apply(chosen);
    }

    static Apply(mode) {
        const chosen = ThemeHandler.MODES.includes(mode) ? mode : "system";
        const dark = chosen === "dark"
            || (chosen === "system" && window.matchMedia(ThemeHandler.QUERY).matches);

        ThemeHandler.mode = chosen;
        document.documentElement.setAttribute("data-bs-theme", dark ? "dark" : "light");

        for (const select of ThemeHandler.selects) select.value = chosen;
    }

    static BindSelect(select) {
        if (!select) return null;
        if (!ThemeHandler.selects.includes(select)) {
            ThemeHandler.selects.push(select);
            select.addEventListener("change", function () { ThemeHandler.Set(select.value); });
        }
        return ThemeHandler.Load();
    }
}

ThemeHandler.Start();
