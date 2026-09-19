console.log("ASSISTANT", "background");

importScripts(
    "./storage-handler.js",
    "./core-messaging.js",
    "./llm-client.js",
    "./memory-tools.js",
    "./fill-loop.js",
    "./compose-loop.js"
);

const messaging = new CoreMessaging();

self.TAB_TIMEOUT_MS = 30000;

chrome.runtime.onMessage.addListener(function (request, sender, sendResponse) {
    if (!request || !request.title) return;

    Route(request).then(sendResponse);
    return true;
});

OpenSessionStorage();
FlushDiffs();

async function Route(request) {
    switch (request.title) {
        case "jobs":
            return messaging.Jobs();
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
            return RunFill(request.params || {});
        case "compose":
            return RunCompose(request.params || {});
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
            chrome.storage.session.setAccessLevel("UNTRUSTED_CONTEXTS");
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
    return { flushed: queued.length - remaining.length };
}

async function RunFill(params) {
    if (!params.tabId) return { error: "no-tab" };

    const state = await TabSend(params.tabId, { title: "inventory" });
    if (!state || state.error) return { error: state ? state.error : "no-content-script" };

    const client = await LlmClient.Create();
    const domain = state.domain;

    const result = await FillLoop.Run({
        client: client,
        domain: domain,
        resume: params.resumeText || "",
        inventory: state.inventory,
        query: function (fieldKey) {
            return MemoryTools.Query(messaging, domain, fieldKey);
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
    return Object.assign(result, { drafted: drafted });
}

async function RunCompose(params) {
    if (!params.tabId) return { error: "no-tab" };

    const state = await TabSend(params.tabId, { title: "inventory" });
    if (!state || state.error) return { error: state ? state.error : "no-content-script" };

    const fields = state.inventory.filter(function (item) { return item.longText && !item.manual; });
    if (!fields.length) return { error: "no-long-fields" };

    const client = await LlmClient.Create();

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

function TabSend(tabId, message) {
    return new Promise(function (resolve) {
        let settled = false;

        const done = function (response) {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            resolve(response ?? { error: "no-response" });
        };

        const timer = setTimeout(function () { done({ error: "tab-timeout" }); }, self.TAB_TIMEOUT_MS);

        try {
            chrome.tabs.sendMessage(tabId, message, function (response) {
                if (chrome.runtime.lastError) {
                    console.log("ASSISTANT", "TabSend", chrome.runtime.lastError.message);
                    done({ error: "no-content-script" });
                    return;
                }
                done(response);
            });
        } catch (e) {
            console.error("ASSISTANT", "TabSend", e);
            done({ error: "no-content-script" });
        }
    });
}
