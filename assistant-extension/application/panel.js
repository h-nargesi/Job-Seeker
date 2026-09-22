console.log("ASSISTANT", "panel.js");

const RANKING_KEYS = [
    "visa_sponsorship", "no_staffing", "remote_only", "salary_floor",
    "seniority_floor", "must_have_language", "contract_type", "relocation",
];
const MEMORY_CAP = 500;

const els = {
    mode: document.getElementById("Mode"),
    modeOverride: document.getElementById("ModeOverride"),
    serverUrl: document.getElementById("ServerUrl"),
    apiKey: document.getElementById("ApiKey"),
    llamaUrl: document.getElementById("LlamaUrl"),
    llamaModel: document.getElementById("LlamaModel"),
    showJobs: document.getElementById("ShowJobs"),
    showMemory: document.getElementById("ShowMemory"),
    jobsView: document.getElementById("JobsView"),
    memoryView: document.getElementById("MemoryView"),
    refreshJobs: document.getElementById("RefreshJobs"),
    jobsStatus: document.getElementById("JobsStatus"),
    jobsList: document.getElementById("JobsList"),
    refreshMemory: document.getElementById("RefreshMemory"),
    memoryScope: document.getElementById("MemoryScope"),
    memoryCap: document.getElementById("MemoryCap"),
    memoryList: document.getElementById("MemoryList"),
    tipScope: document.getElementById("TipScope"),
    tipRankingKey: document.getElementById("TipRankingKey"),
    tipFieldKey: document.getElementById("TipFieldKey"),
    tipValue: document.getElementById("TipValue"),
    tipNote: document.getElementById("TipNote"),
    saveTip: document.getElementById("SaveTip"),
    chatLog: document.getElementById("ChatLog"),
};

const state = { pageState: null, override: "auto", jobs: [], memoryLoaded: false };

els.modeOverride.addEventListener("change", async function () {
    state.override = els.modeOverride.value;
    await StorageHandler.SetSession(StorageHandler.MODE_OVERRIDE, state.override);
    RenderMode();
    RenderTipScope();
});

els.showJobs.addEventListener("click", function () { SwitchView(true); });
els.showMemory.addEventListener("click", function () { SwitchView(false); });

els.refreshJobs.addEventListener("click", RefreshJobs);
els.refreshMemory.addEventListener("click", RefreshMemory);
els.memoryScope.addEventListener("change", RefreshMemory);
els.tipScope.addEventListener("change", RenderTipFields);
els.saveTip.addEventListener("click", SaveTip);

SaveOnEnter(els.serverUrl, value => { StorageHandler.ServerUrl = value; });
SaveOnEnter(els.apiKey, value => { StorageHandler.ApiKey = value; });
SaveOnEnter(els.llamaUrl, value => { StorageHandler.LlamaUrl = value; });
SaveOnEnter(els.llamaModel, value => { StorageHandler.LlamaModel = value; });

function SaveOnEnter(input, save) {
    input.addEventListener("keyup", function (event) {
        event.preventDefault();
        if (event.keyCode === 13) save(input.value.toString().trim());
    });
}

function SwitchView(jobs) {
    els.jobsView.style.display = jobs ? "" : "none";
    els.memoryView.style.display = jobs ? "none" : "";
    els.showJobs.classList.toggle("active", jobs);
    els.showMemory.classList.toggle("active", !jobs);

    if (!jobs && !state.memoryLoaded) {
        state.memoryLoaded = true;
        RefreshMemory();
    }
}

function EffectiveMode() {
    if (state.override === "job_detail" || state.override === "apply_form") return state.override;
    return state.pageState?.mode ?? "apply_form";
}

function RenderMode() {
    const mode = EffectiveMode();
    const domain = state.pageState?.domain ?? "";
    els.mode.textContent = ` — ${mode}${domain ? " (" + domain + ")" : ""}`;
}

async function LoadMode() {
    state.override = (await StorageHandler.GetSession(StorageHandler.MODE_OVERRIDE, "auto")) || "auto";
    els.modeOverride.value = state.override;

    state.pageState = null;
    try {
        const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
        if (tabs && tabs.length) {
            state.pageState = await TabRequest(tabs[0].id, { title: "page-state" });
        }
    } catch (e) {
        console.log("ASSISTANT", "LoadMode", e);
    }

    RenderMode();
    RenderTipScope();
}

function TabRequest(tabId, message) {
    return new Promise(function (resolve) {
        try {
            chrome.tabs.sendMessage(tabId, message, function (response) {
                resolve(chrome.runtime.lastError ? null : response);
            });
        } catch (e) {
            resolve(null);
        }
    });
}

async function LoadSettings() {
    els.serverUrl.value = await StorageHandler.ServerUrlAsync();
    els.apiKey.value = await StorageHandler.ApiKeyAsync();
    els.llamaUrl.value = await StorageHandler.LlamaUrlAsync();
    els.llamaModel.value = await StorageHandler.LlamaModelAsync();
}

async function RefreshJobs() {
    els.jobsStatus.textContent = "loading ...";
    const result = await BackgroundMessaging.Message("jobs");

    if (!Array.isArray(result)) {
        state.jobs = [];
        els.jobsStatus.textContent = "error: " + (result?.error ?? "unknown");
        RenderJobs();
        return;
    }

    state.jobs = result;
    els.jobsStatus.textContent = `${result.length} attention job(s)`;
    RenderJobs();
}

function RenderJobs() {
    els.jobsList.innerHTML = "";

    for (const job of state.jobs) {
        const row = document.createElement("div");
        row.className = "job";

        const title = document.createElement("div");
        title.textContent = `#${job.jobId} ${job.title ?? ""} (score ${job.aiScore ?? "-"})`;
        row.appendChild(title);

        if (job.pendingProposal) {
            const warn = document.createElement("div");
            warn.className = "warn";
            warn.textContent = "pending resume proposal — review before sending";
            row.appendChild(warn);
        }

        const actions = document.createElement("div");
        actions.appendChild(JobButton("Open", function () { chrome.tabs.create({ url: job.url }); }));
        actions.appendChild(JobButton("Applied", function () { MarkApplied(job.jobId); }));
        actions.appendChild(JobButton("Fill current tab", function () { FillCurrentTab(job); }));
        actions.appendChild(JobButton("Compose", function () { ComposeUI.ComposeFor(job); }));
        row.appendChild(actions);

        els.jobsList.appendChild(row);
    }
}

function JobButton(text, onClick) {
    const button = document.createElement("button");
    button.textContent = text;
    button.addEventListener("click", onClick);
    return button;
}

async function MarkApplied(jobId) {
    const result = await BackgroundMessaging.Message("applied", { jobId });
    els.jobsStatus.textContent = result?.error ? "applied error: " + result.error : `marked applied #${jobId}`;
}

async function FillCurrentTab(job) {
    els.jobsStatus.textContent = "filling ...";

    let tabId = null;
    try {
        const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
        if (tabs && tabs.length) tabId = tabs[0].id;
    } catch (e) {
        tabId = null;
    }

    if (!tabId) {
        els.jobsStatus.textContent = "no active tab";
        return;
    }

    const result = await BackgroundMessaging.Message("fill", {
        tabId: tabId,
        jobId: job.jobId,
        resumeText: job.resumeText ?? "",
    }, BackgroundMessaging.LONG_TIMEOUT);

    if (result?.error) {
        els.jobsStatus.textContent = "fill error: " + result.error
            + (result.drafted ? ` (${result.drafted} long answer(s) applied)` : "");
    }
    else els.jobsStatus.textContent = `filled ${result?.filled ?? 0} field(s), ${result?.writes ?? 0} memory write(s), ${result?.drafted ?? 0} long answer(s) — review, then submit yourself`;
}

async function RefreshMemory() {
    const scope = els.memoryScope.value || null;
    const result = await BackgroundMessaging.Message("memory-list", { scope: scope });
    if (!Array.isArray(result)) {
        els.memoryList.textContent = "error: " + (result?.error ?? "unknown");
        return;
    }
    RenderMemory(result);
}

function RenderMemory(rows) {
    const confirmed = rows.filter(row => row.confirmed).length;
    els.memoryCap.textContent = confirmed > MEMORY_CAP
        ? `memorycap: ${confirmed} confirmed rows — only ${MEMORY_CAP} are injected; distill the list`
        : "";

    els.memoryList.innerHTML = "";

    for (const row of rows) {
        const item = document.createElement("div");
        item.className = "memory-row";

        const text = document.createElement("div");
        text.textContent = `[${row.scope}/${row.kind}${row.confirmed ? " confirmed" : " pending"}] `
            + `${row.fieldKey} = ${String(row.value).slice(0, 80)} (${row.agencyDomain}, used ${row.useCount ?? 0})`;
        item.appendChild(text);

        const actions = document.createElement("div");
        actions.appendChild(JobButton(row.confirmed ? "Unconfirm" : "Confirm", function () { MemoryAction("memory-confirm", { id: row.memoryID, confirmed: !row.confirmed }); }));
        actions.appendChild(JobButton("Edit", function () { EditMemory(row); }));
        actions.appendChild(JobButton("Delete", function () { MemoryAction("memory-delete", { id: row.memoryID }); }));
        item.appendChild(actions);

        els.memoryList.appendChild(item);
    }
}

async function MemoryAction(title, params) {
    await BackgroundMessaging.Message(title, params);
    RefreshMemory();
}

async function EditMemory(row) {
    const value = prompt("New value", row.value);
    if (value === null) return;

    const note = prompt("Note", row.note ?? "") ?? "";
    await BackgroundMessaging.Message("memory-edit", {
        id: row.memoryID,
        row: { value: value, note: note },
    });
    RefreshMemory();
}

function RenderTipScope() {
    const mode = EffectiveMode();
    els.tipScope.innerHTML = "";

    const scopes = mode === "job_detail" ? ["Ranking", "Resume"] : ["Apply"];
    for (const scope of scopes) {
        const option = document.createElement("option");
        option.value = scope;
        option.textContent = scope.toLowerCase();
        els.tipScope.appendChild(option);
    }

    RenderTipFields();
}

function RenderTipFields() {
    const ranking = els.tipScope.value === "Ranking";
    els.tipRankingKey.style.display = ranking ? "" : "none";
    els.tipFieldKey.style.display = ranking ? "none" : "";

    if (ranking && !els.tipRankingKey.options.length) {
        for (const key of RANKING_KEYS) {
            const option = document.createElement("option");
            option.value = key;
            option.textContent = key;
            els.tipRankingKey.appendChild(option);
        }
    }
}

async function SaveTip() {
    const scope = els.tipScope.value;
    const fieldKey = scope === "Ranking" ? els.tipRankingKey.value : FormInventory.NormalizeKey(els.tipFieldKey.value, "");
    const value = els.tipValue.value.trim();
    const note = els.tipNote.value.trim();

    if (!fieldKey || !value) {
        els.chatLog.textContent = "lesson needs a field and a value";
        return;
    }

    const domain = scope === "Apply" ? (state.pageState?.domain ?? "*") : "*";
    const result = await BackgroundMessaging.Message("tip", {
        scope: scope,
        domain: domain,
        fieldKey: fieldKey,
        fieldLabel: scope === "Ranking" ? undefined : FormInventory.CleanLabel(els.tipFieldKey.value),
        value: value,
        note: note || undefined,
    });

    els.chatLog.textContent = result?.error ? "lesson error: " + result.error : `saved ${scope} lesson`;
    els.tipValue.value = "";
    els.tipNote.value = "";
    if (state.memoryLoaded) RefreshMemory();
}

async function LoadChatLog() {
    const log = (await StorageHandler.GetSession(StorageHandler.CHAT_LOG, [])) || [];
    els.chatLog.textContent = log.length
        ? "recent lessons: " + log.map(entry => `${entry.scope}:${entry.fieldKey}`).join(", ")
        : "no lessons this session";
}

async function LoadData() {
    BackgroundMessaging.Message("flush-diffs");
    await Promise.all([
        LoadSettings(),
        LoadMode(),
        ComposeUI.Init(),
        LoadChatLog(),
        RefreshJobs(),
    ]);
}

LoadData();
