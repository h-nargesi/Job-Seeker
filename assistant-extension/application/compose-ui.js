console.log("ASSISTANT", "compose-ui");

class ComposeUI {

    static els = {};

    static Bind() {
        ComposeUI.els = {
            status: document.getElementById("ComposeStatus"),
            list: document.getElementById("ComposeList"),
        };
    }

    static Status(text) {
        if (ComposeUI.els.status) ComposeUI.els.status.textContent = text;
    }

    static async Init() {
        ComposeUI.Bind();
        await ComposeUI.Reload();
    }

    static async Reload() {
        const result = await BackgroundMessaging.Message("compose-list");
        ComposeUI.Render(Array.isArray(result?.drafts) ? result.drafts : []);
    }

    static async ComposeFor(job) {
        const tabId = await ComposeUI.ActiveTabId();
        if (!tabId) {
            ComposeUI.Status("no active tab");
            return;
        }

        let guidance = "";
        try { guidance = prompt("Guidance for the long answers (optional)", "") || ""; } catch (e) { guidance = ""; }

        ComposeUI.Status("composing ...");

        const result = await BackgroundMessaging.Message("compose", {
            tabId: tabId,
            jobId: job.jobId,
            resumeText: job.resumeText ?? "",
            guidance: guidance,
        }, BackgroundMessaging.LONG_TIMEOUT);

        if (result?.error) {
            ComposeUI.Status("compose error: " + result.error);
            return;
        }

        ComposeUI.Render(Array.isArray(result?.drafts) ? result.drafts : []);
        ComposeUI.Status("drafts are pending — accept before Fill fills them");
    }

    static Render(drafts) {
        if (!ComposeUI.els.list) return;
        ComposeUI.els.list.innerHTML = "";

        if (!drafts.length) {
            ComposeUI.els.list.textContent = "no drafts this session";
            return;
        }

        for (const draft of drafts) ComposeUI.els.list.appendChild(ComposeUI.Item(draft));
    }

    static Item(draft) {
        const item = document.createElement("div");
        item.className = "draft";

        const head = document.createElement("div");
        head.textContent = `${draft.fieldLabel || draft.fieldKey} (${draft.domain}) — `
            + (draft.accepted ? "accepted — Fill current tab applies it" : "pending review");
        head.className = draft.accepted ? "muted" : "warn";
        item.appendChild(head);

        const area = document.createElement("textarea");
        area.rows = 6;
        area.value = draft.text;
        if (draft.accepted) area.readOnly = true;
        item.appendChild(area);

        const actions = document.createElement("div");
        if (!draft.accepted) {
            actions.appendChild(ComposeUI.Button("Accept", function () { ComposeUI.Accept(draft, area); }));
        }
        actions.appendChild(ComposeUI.Button("Reject", function () { ComposeUI.Reject(draft); }));
        item.appendChild(actions);

        return item;
    }

    static Button(text, onClick) {
        const button = document.createElement("button");
        button.textContent = text;
        button.addEventListener("click", onClick);
        return button;
    }

    static async Accept(draft, area) {
        const result = await BackgroundMessaging.Message("compose-accept", { id: draft.id, text: area.value });
        ComposeUI.Status(result?.error ? "accept error: " + result.error : "accepted — press Fill current tab");
        await ComposeUI.Reload();
    }

    static async Reject(draft) {
        const result = await BackgroundMessaging.Message("compose-reject", { id: draft.id });
        ComposeUI.Status(result?.error ? "reject error: " + result.error : "draft rejected");
        await ComposeUI.Reload();
    }

    static async ActiveTabId() {
        try {
            const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
            if (tabs && tabs.length) return tabs[0].id;
        } catch (e) {
            console.log("ASSISTANT", "ComposeUI", e);
        }
        return null;
    }
}
