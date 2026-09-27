console.log("ASSISTANT", "background");

importScripts(
    "./storage-handler.js",
    "./logger.js",
    "./core-messaging.js",
    "./llm-client.js",
    "./memory-tools.js",
    "./fill-loop.js",
    "./compose-loop.js",
    "./lesson-loop.js"
);

const messaging = new CoreMessaging();

self.TAB_TIMEOUT_MS = 30000;
self.LLM_BUSY = false;

async function WithLlm(run) {
    if (self.LLM_BUSY) return { error: "llm-busy" };
    self.LLM_BUSY = true;
    try {
        return await run();
    } finally {
        self.LLM_BUSY = false;
    }
}

self.addEventListener("unhandledrejection", function (event) {
    AssistantLog.Write("error", "sw", "unhandledrejection: " + String(event.reason ?? ""));
});

chrome.runtime.onMessage.addListener(function (request, sender, sendResponse) {
    if (!request || !request.title) return;

    Route(request).then(sendResponse);
    return true;
});

OpenSessionStorage();
FlushDiffs();

async function Route(request) {
    switch (request.title) {
        case "job":
            return messaging.Job(request.params?.jobId);
        case "applied":
            return messaging.Applied(request.params?.jobId);
        case "memory-list":
            return messaging.MemoryList(request.params?.scope, request.params?.confirmed);
        case "memory-save":
            return messaging.MemorySave(request.params?.row);
        case "memory-edit":
            return messaging.MemoryEdit(request.params?.id, request.params?.row);
        case "memory-confirm":
            return messaging.MemoryConfirm(request.params?.id, request.params?.confirmed);
        case "memory-delete":
            return messaging.MemoryDelete(request.params?.id);
        case "tip":
            return SaveTip(request.params || {});
        case "flush-diffs":
            return FlushDiffs();
        case "fill":
            return WithLlm(function () { return RunFill(request.params || {}); });
        case "compose":
            return WithLlm(function () { return RunCompose(request.params || {}); });
        case "lesson-extract":
            return WithLlm(function () { return RunLessonExtract(request.params || {}); });
        case "compose-list":
            return { drafts: await ComposeStore.All() };
        case "compose-accept":
            return ComposeStore.Accept(request.params?.id, request.params?.text);
        case "compose-reject":
            return ComposeStore.Reject(request.params?.id);
        default:
            return { error: "unknown-title" };
    }
}

function OpenSessionStorage() {
    try {
        if (chrome.storage.session && chrome.storage.session.setAccessLevel)
            chrome.storage.session.setAccessLevel({ accessLevel: "UNTRUSTED_CONTEXTS" });
    } catch (e) {
        console.error("ASSISTANT", "OpenSessionStorage", e);
    }
}

async function SaveTip(params) {
    if (!params.scope || !params.fieldKey || !params.value) return { error: "validation" };

    const result = await MemoryTools.SaveTip(messaging, params);
    if (result && result.error) return result;

    const log = (await StorageHandler.GetSession(StorageHandler.CHAT_LOG, [])) || [];
    log.push({ at: Date.now(), scope: params.scope, fieldKey: params.fieldKey, value: params.value });
    await StorageHandler.SetSession(StorageHandler.CHAT_LOG, log.slice(-50));

    return result;
}

async function FlushDiffs() {
    const queued = (await StorageHandler.GetSession(StorageHandler.PENDING_DIFFS, [])) || [];
    if (!queued.length) return { flushed: 0 };

    const remaining = [];
    for (const diff of queued) {
        const result = await MemoryTools.SaveCorrection(messaging, diff);
        if (result && result.error) remaining.push(diff);
    }

    await StorageHandler.SetSession(StorageHandler.PENDING_DIFFS, remaining);
    AssistantLog.Write(remaining.length ? "warn" : "info", "flush-diffs",
        `flushed ${queued.length - remaining.length}/${queued.length}`);
    return { flushed: queued.length - remaining.length };
}

async function RunFill(params) {
    if (!params.tabId) {
        AssistantLog.Write("warn", "fill", "no-tab");
        return { error: "no-tab" };
    }

    const state = await TabSend(params.tabId, { title: "inventory" });
    if (!state || state.error) {
        const error = state ? state.error : "no-content-script";
        AssistantLog.Write("warn", "fill", error);
        return { error: error };
    }

    const client = await LlmClient.Create();
    const domain = state.domain;
    const snapshot = MemoryTools.Snapshot(messaging);

    AssistantLog.Write("info", "fill", `start job ${params.jobId ?? "-"} (${domain})`);

    const result = await FillLoop.Run({
        client: client,
        domain: domain,
        resume: params.resumeText || "",
        inventory: state.inventory,
        query: function (fieldKey) {
            return MemoryTools.Query(messaging, domain, fieldKey, snapshot);
        },
        write: function (fact) {
            const entry = state.inventory.find(function (item) { return item.fieldKey === fact.fieldKey; });
            return MemoryTools.SaveApplyFact(messaging, domain, fact.fieldKey, entry?.label, fact.value, fact.note);
        },
        fill: function (fieldId, value) {
            return TabSend(params.tabId, { title: "apply-fill", params: { fieldId: fieldId, value: value } });
        },
        bump: function (id) {
            messaging.MemoryBump(id);
        },
    });

    const drafted = await ApplyAcceptedDrafts(params.tabId, domain, state.inventory);

    if (result.error) {
        AssistantLog.Write("error", "fill",
            `end job ${params.jobId ?? "-"}: ${result.error} (filled ${result.filled}, writes ${result.writes}, drafted ${drafted})`);
    }
    else {
        AssistantLog.Write("info", "fill",
            `end job ${params.jobId ?? "-"}: filled ${result.filled}, writes ${result.writes}, drafted ${drafted}`);
    }

    return Object.assign(result, { drafted: drafted });
}

async function RunCompose(params) {
    if (!params.tabId) {
        AssistantLog.Write("warn", "compose", "no-tab");
        return { error: "no-tab" };
    }

    const state = await TabSend(params.tabId, { title: "inventory" });
    if (!state || state.error) {
        const error = state ? state.error : "no-content-script";
        AssistantLog.Write("warn", "compose", error);
        return { error: error };
    }

    const fields = state.inventory.filter(function (item) { return item.longText && !item.manual; });
    if (!fields.length) {
        AssistantLog.Write("warn", "compose", "no-long-fields");
        return { error: "no-long-fields" };
    }

    const client = await LlmClient.Create();

    AssistantLog.Write("info", "compose",
        `start job ${params.jobId ?? "-"} (${state.domain}, ${fields.length} long field(s))`);

    const result = await ComposeLoop.Run({
        client: client,
        domain: state.domain,
        resume: params.resumeText || "",
        fields: fields,
        guidance: params.guidance || "",
    });
    if (result.error) return result;

    const drafts = ComposeStore.Merge(await ComposeStore.All(), result.drafts, state.domain);
    await ComposeStore.Save(drafts);
    AssistantLog.Write("info", "compose", `end job ${params.jobId ?? "-"}: ${drafts.length} draft(s)`);
    return { drafts: drafts };
}

async function ApplyAcceptedDrafts(tabId, domain, inventory) {
    const drafts = await ComposeStore.All();
    let drafted = 0;

    for (const draft of drafts) {
        if (!draft.accepted || draft.domain !== domain) continue;

        const entry = inventory.find(function (item) {
            return item.longText && !item.manual && item.fieldKey === draft.fieldKey;
        });
        if (!entry) continue;

        const result = await TabSend(tabId, { title: "apply-fill", params: { fieldId: entry.fieldId, value: draft.text } });
        if (result && result.ok) drafted++;
    }

    return drafted;
}

async function RunLessonExtract(params) {
    if (!params.text || !String(params.text).trim()) return { error: "validation" };

    const mode = params.mode === "job_detail" ? "job_detail" : "apply_form";

    let inventoryKeys = [];
    if (mode === "apply_form" && params.tabId) {
        const state = await TabSend(params.tabId, { title: "inventory" });
        if (state && !state.error && Array.isArray(state.inventory)) {
            inventoryKeys = state.inventory
                .map(function (item) { return item.fieldKey; })
                .filter(Boolean);
        }
    }

    const confirmedKeys = await ConfirmedKeys(mode);

    AssistantLog.Write("info", "lesson", "extract (" + mode + ", " + (params.domain || "*") + ")");

    const client = await LlmClient.Create();
    const result = await LessonLoop.Run({
        client: client,
        text: params.text,
        mode: mode,
        domain: params.domain || "*",
        inventoryKeys: inventoryKeys,
        confirmedKeys: confirmedKeys,
    });

    if (result && result.error) {
        AssistantLog.Write("error", "lesson", result.error);
        return result;
    }

    AssistantLog.Write("info", "lesson",
        "reply + " + result.candidates.length + " candidate(s), " + result.dropped.length + " dropped");
    return result;
}

async function ConfirmedKeys(mode) {
    try {
        const rows = await messaging.MemoryList(mode === "job_detail" ? null : "Apply", true);
        if (!Array.isArray(rows)) return [];

        return rows
            .filter(function (row) {
                return mode !== "job_detail" || row.scope === "Ranking" || row.scope === "Resume";
            })
            .map(function (row) { return row.scope + "/" + row.fieldKey; });
    } catch (e) {
        return [];
    }
}

function TabSend(tabId, message) {
    return new Promise(function (resolve) {
        let settled = false;

        const done = function (response) {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            if (!response) AssistantLog.Write("warn", "tab", "no-response");
            resolve(response ?? { error: "no-response" });
        };

        const timer = setTimeout(function () {
            AssistantLog.Write("warn", "tab", "tab-timeout");
            done({ error: "tab-timeout" });
        }, self.TAB_TIMEOUT_MS);

        try {
            chrome.tabs.sendMessage(tabId, message, function (response) {
                if (chrome.runtime.lastError) {
                    console.log("ASSISTANT", "TabSend", chrome.runtime.lastError.message);
                    AssistantLog.Write("warn", "tab", "no-content-script");
                    done({ error: "no-content-script" });
                    return;
                }
                done(response);
            });
        } catch (e) {
            console.error("ASSISTANT", "TabSend", e);
            AssistantLog.Write("warn", "tab", "no-content-script");
            done({ error: "no-content-script" });
        }
    });
}
