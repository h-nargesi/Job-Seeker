console.log("ASSISTANT", "panel.js");

const els = {
    version: document.getElementById("Version"),
    mode: document.getElementById("Mode"),
    modeOverride: document.getElementById("ModeOverride"),
    showJobs: document.getElementById("ShowJobs"),
    showMemory: document.getElementById("ShowMemory"),
    jobsView: document.getElementById("JobsView"),
    memoryView: document.getElementById("MemoryView"),
    jobId: document.getElementById("JobId"),
    fromPage: document.getElementById("FromPage"),
    loadJob: document.getElementById("LoadJob"),
    jobInfo: document.getElementById("JobInfo"),
    openJob: document.getElementById("OpenJob"),
    markApplied: document.getElementById("MarkApplied"),
    fillJob: document.getElementById("FillJob"),
    composeJob: document.getElementById("ComposeJob"),
    jobsStatus: document.getElementById("JobsStatus"),
};

Dropdown.Attach(els.modeOverride);

const state = { pageState: null, override: "auto", job: null, memoryLoaded: false };

els.modeOverride.addEventListener("change", async function () {
    state.override = els.modeOverride.value;
    await StorageHandler.SetSession(StorageHandler.MODE_OVERRIDE, state.override);
    RenderMode();
    MemoryUi.RenderTipScope();
});

els.showJobs.addEventListener("click", function () { SwitchView(true); });
els.showMemory.addEventListener("click", function () { SwitchView(false); });

els.jobId.addEventListener("keyup", function (event) {
    if (event.keyCode === 13) LoadJob();
});
els.jobId.addEventListener("input", function () {
    els.jobId.value = els.jobId.value.replace(/\D+/g, "");
});
els.fromPage.addEventListener("click", FromPage);
els.loadJob.addEventListener("click", LoadJob);
els.openJob.addEventListener("click", OpenJob);
els.markApplied.addEventListener("click", MarkApplied);
els.fillJob.addEventListener("click", FillJob);
els.composeJob.addEventListener("click", ComposeJob);

function SwitchView(jobs) {
    els.jobsView.style.display = jobs ? "" : "none";
    els.memoryView.style.display = jobs ? "none" : "";
    els.showJobs.classList.toggle("active", jobs);
    els.showMemory.classList.toggle("active", !jobs);

    if (!jobs && !state.memoryLoaded) {
        state.memoryLoaded = true;
        MemoryUi.Refresh();
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
    MemoryUi.RenderTipScope();
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

function ParseJobId(text) {
    const value = (text ?? "").trim();
    return /^\d+$/.test(value) ? Number(value) : null;
}

async function LoadJob() {
    const jobId = ParseJobId(els.jobId.value);
    if (!jobId) {
        els.jobsStatus.textContent = "enter a job id";
        return;
    }

    els.jobsStatus.textContent = "loading ...";
    const result = await BackgroundMessaging.Message("job", { jobId });

    if (!result || result.error) {
        state.job = null;
        RenderJobInfo();
        els.jobsStatus.textContent = result?.status === 404
            ? "job not found"
            : "error: " + (result?.error ?? "unknown");
        return;
    }

    state.job = result;
    RenderJobInfo();
    els.jobsStatus.textContent = "";
}

function RenderJobInfo() {
    els.jobInfo.innerHTML = "";
    if (!state.job) return;

    const line = document.createElement("div");
    line.textContent = `#${state.job.jobId} ${state.job.title ?? ""} (score ${state.job.aiScore ?? "-"})`;
    els.jobInfo.appendChild(line);

    if (state.job.pendingProposal) {
        const warn = document.createElement("div");
        warn.className = "warn";
        warn.textContent = "pending resume proposal — review before sending";
        els.jobInfo.appendChild(warn);
    }
}

function JobIdFromUrl(url) {
    if (typeof url !== "string" || !url) return null;
    try {
        const match = new URL(url).pathname.match(/^\/job\/get\/(\d+)\/?$/);
        return match ? Number(match[1]) : null;
    } catch (e) {
        return null;
    }
}

async function FromPage() {
    let url;
    try {
        const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
        url = tabs && tabs.length ? tabs[0].url : undefined;
    } catch (e) {
        url = undefined;
    }

    const jobId = JobIdFromUrl(url);
    if (!jobId) {
        els.jobsStatus.textContent = typeof url === "string" && url
            ? "current page is not a job details page"
            : "cannot read the current tab url";
        return;
    }

    els.jobId.value = String(jobId);
    await LoadJob();
}

function OpenJob() {
    if (!state.job) {
        els.jobsStatus.textContent = "load the job first";
        return;
    }
    chrome.tabs.create({ url: state.job.url });
}

async function MarkApplied() {
    const jobId = ParseJobId(els.jobId.value);
    if (!jobId) {
        els.jobsStatus.textContent = "enter a job id";
        return;
    }

    const result = await BackgroundMessaging.Message("applied", { jobId });
    els.jobsStatus.textContent = result?.error ? "applied error: " + result.error : `marked applied #${jobId}`;
}

function FillJob() {
    if (!state.job) {
        els.jobsStatus.textContent = "load the job first";
        return;
    }
    FillCurrentTab(state.job);
}

function ComposeJob() {
    if (!state.job) {
        els.jobsStatus.textContent = "load the job first";
        return;
    }
    ComposeUI.ComposeFor(state.job);
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
        const text = "fill error: " + result.error
            + (result.drafted ? ` (${result.drafted} long answer(s) applied)` : "");
        els.jobsStatus.textContent = text;
        ChatUi.Report(text);
    }
    else {
        els.jobsStatus.textContent = `filled ${result?.filled ?? 0} field(s), ${result?.writes ?? 0} memory write(s), ${result?.drafted ?? 0} long answer(s) — review, then submit yourself`;
        if (result?.content) ChatUi.Report(result.content);
    }
}

async function LoadData() {
    BackgroundMessaging.Message("flush-diffs");
    const manifest = chrome.runtime.getManifest();
    els.version.textContent = manifest.version_name ?? manifest.version;
    MemoryUi.Init({
        mode: EffectiveMode,
        domain: () => state.pageState?.domain ?? "*",
        memoryLoaded: () => state.memoryLoaded,
    });
    await Promise.all([
        LoadMode(),
        ComposeUI.Init(),
        ChatUi.Init({ mode: EffectiveMode, domain: () => state.pageState?.domain ?? "*" }),
        MemoryUi.LoadChatLog(),
    ]);
}

LoadData();
