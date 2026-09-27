console.log("ASSISTANT", "settings.js");

const els = {
    version: document.getElementById("Version"),
    serverUrl: document.getElementById("ServerUrl"),
    apiKey: document.getElementById("ApiKey"),
    llamaUrl: document.getElementById("LlamaUrl"),
    llamaModel: document.getElementById("LlamaModel"),
    themeMode: document.getElementById("ThemeMode"),
    openPanel: document.getElementById("OpenPanel"),
    logsFilter: document.getElementById("LogsFilter"),
    logsRefresh: document.getElementById("LogsRefresh"),
    logsClear: document.getElementById("LogsClear"),
    logsDownload: document.getElementById("LogsDownload"),
    logsStatus: document.getElementById("LogsStatus"),
    logsList: document.getElementById("LogsList"),
};

SaveOnEnter(els.serverUrl, value => { StorageHandler.ServerUrl = value; });
SaveOnEnter(els.apiKey, value => { StorageHandler.ApiKey = value; });
SaveOnEnter(els.llamaUrl, value => { StorageHandler.LlamaUrl = value; });
SaveOnEnter(els.llamaModel, value => { StorageHandler.LlamaModel = value; });
Dropdown.Attach(els.themeMode);
Dropdown.Attach(els.logsFilter);
ThemeHandler.BindSelect(els.themeMode);

function SaveOnEnter(input, save) {
    input.addEventListener("keyup", function (event) {
        event.preventDefault();
        if (event.keyCode === 13) save(input.value.toString().trim());
    });
}

els.openPanel.addEventListener("click", async function () {
    try {
        const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
        if (tabs && tabs.length) await chrome.sidePanel.open({ tabId: tabs[0].id });
    } catch (e) {
        console.error("ASSISTANT", "OpenPanel", e);
    }
});

els.logsFilter.addEventListener("change", RenderLogs);
els.logsRefresh.addEventListener("click", RenderLogs);
els.logsClear.addEventListener("click", ClearLogs);
els.logsDownload.addEventListener("click", DownloadLogs);

async function RenderLogs() {
    const filter = els.logsFilter.value;
    const entries = await AssistantLog.All();
    const visible = (filter ? entries.filter(entry => entry.level === filter) : entries)
        .slice(-100).reverse();

    els.logsStatus.textContent = entries.length
        ? `${visible.length}/${entries.length} entry(ies)`
        : "no entries";

    els.logsList.innerHTML = "";
    for (const entry of visible) els.logsList.appendChild(LogRow(entry));
}

function LogRow(entry) {
    const row = document.createElement("div");
    row.className = entry.level === "error" ? "log-error" : entry.level === "warn" ? "warn" : "muted";

    const time = new Date(entry.t).toISOString().replace("T", " ").slice(0, 19);
    row.textContent = `${time} ${entry.level} ${entry.where}: ${entry.detail}`;
    return row;
}

async function ClearLogs() {
    await AssistantLog.Clear();
    await RenderLogs();
}

async function DownloadLogs() {
    const entries = await AssistantLog.All();
    const lines = entries.map(function (entry) {
        const time = new Date(entry.t).toISOString().replace("T", " ").slice(0, 19);
        return `${time} ${entry.level} ${entry.where}: ${entry.detail}`;
    });

    const blob = new Blob([lines.join("\n") + "\n"], { type: "text/plain" });
    const url = URL.createObjectURL(blob);

    const link = document.createElement("a");
    link.href = url;
    link.download = "assistant-" + new Date().toISOString().slice(0, 10).replace(/-/g, "") + ".log";
    link.click();

    URL.revokeObjectURL(url);
    els.logsStatus.textContent = `${entries.length} entry(ies) exported`;
}

async function LoadSettings() {
    const manifest = chrome.runtime.getManifest();
    els.version.textContent = manifest.version_name ?? manifest.version;
    els.serverUrl.value = await StorageHandler.ServerUrlAsync();
    els.apiKey.value = await StorageHandler.ApiKeyAsync();
    els.llamaUrl.value = await StorageHandler.LlamaUrlAsync();
    els.llamaModel.value = await StorageHandler.LlamaModelAsync();
}

LoadSettings();
RenderLogs();
