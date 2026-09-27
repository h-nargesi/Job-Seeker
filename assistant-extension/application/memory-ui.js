console.log("ASSISTANT", "memory-ui");

class MemoryUi {

    static MEMORY_CAP = 500;
    static SCOPES = ["Apply", "Ranking", "Resume"];
    static KINDS = ["Tip", "Correction"];

    static els = {};
    static hooks = { mode: null, domain: null, memoryLoaded: null };
    static editing = null;

    static Init(hooks) {
        MemoryUi.Bind();
        if (hooks) MemoryUi.hooks = hooks;

        Dropdown.Attach(MemoryUi.els.scope);
        Dropdown.Attach(MemoryUi.els.tipScope);
        Dropdown.Attach(MemoryUi.els.tipRankingKey);
        Dropdown.Attach(MemoryUi.els.tipKind);
        MemoryUi.els.tipKind.options = MemoryUi.KINDS.map(kind => ({ value: kind, text: kind }));

        MemoryUi.els.refresh.addEventListener("click", MemoryUi.Refresh);
        MemoryUi.els.scope.addEventListener("change", MemoryUi.Refresh);
        MemoryUi.els.tipScope.addEventListener("change", MemoryUi.RenderTipFields);
        MemoryUi.els.saveTip.addEventListener("click", MemoryUi.SaveTip);
        MemoryUi.els.tipCancel.addEventListener("click", MemoryUi.ResetTipForm);
    }

    static Bind() {
        MemoryUi.els = {
            refresh: document.getElementById("RefreshMemory"),
            scope: document.getElementById("MemoryScope"),
            cap: document.getElementById("MemoryCap"),
            list: document.getElementById("MemoryList"),
            form: document.getElementById("TipForm"),
            formTitle: document.getElementById("TipFormTitle"),
            tipScope: document.getElementById("TipScope"),
            tipRankingKey: document.getElementById("TipRankingKey"),
            tipFieldKey: document.getElementById("TipFieldKey"),
            tipDomain: document.getElementById("TipDomain"),
            tipDomainGroup: document.getElementById("TipDomainGroup"),
            tipFieldLabel: document.getElementById("TipFieldLabel"),
            tipFieldLabelGroup: document.getElementById("TipFieldLabelGroup"),
            tipKind: document.getElementById("TipKind"),
            tipKindGroup: document.getElementById("TipKindGroup"),
            tipConfirmed: document.getElementById("TipConfirmed"),
            tipConfirmedGroup: document.getElementById("TipConfirmedGroup"),
            tipValue: document.getElementById("TipValue"),
            tipNote: document.getElementById("TipNote"),
            saveTip: document.getElementById("SaveTip"),
            tipCancel: document.getElementById("TipCancel"),
            chatLog: document.getElementById("ChatLog"),
        };
    }

    static EffectiveMode() {
        return MemoryUi.hooks.mode ? MemoryUi.hooks.mode() : "apply_form";
    }

    static async Refresh() {
        const scope = MemoryUi.els.scope.value || null;
        const result = await BackgroundMessaging.Message("memory-list", { scope: scope });
        if (!Array.isArray(result)) {
            MemoryUi.els.list.textContent = "error: " + (result?.error ?? "unknown");
            return;
        }
        MemoryUi.Render(result);
    }

    static Render(rows) {
        const confirmed = rows.filter(row => row.confirmed).length;
        MemoryUi.els.cap.textContent = confirmed > MemoryUi.MEMORY_CAP
            ? `memorycap: ${confirmed} confirmed rows — only ${MemoryUi.MEMORY_CAP} are injected; distill the list`
            : "";

        MemoryUi.els.list.innerHTML = "";

        for (const row of rows) {
            const item = document.createElement("div");
            item.className = "memory-row";
            if (MemoryUi.editing && MemoryUi.editing.memoryID === row.memoryID) item.classList.add("editing");

            const text = document.createElement("div");
            text.textContent = `[${row.scope}/${row.kind}${row.confirmed ? " confirmed" : " pending"}] `
                + `${row.fieldKey} = ${String(row.value).slice(0, 80)} (${row.agencyDomain}, used ${row.useCount ?? 0})`;
            item.appendChild(text);

            const actions = document.createElement("div");
            actions.appendChild(MemoryUi.Button(row.confirmed ? "Unconfirm" : "Confirm",
                function () { MemoryUi.MemoryAction("memory-confirm", { id: row.memoryID, confirmed: !row.confirmed }); }));
            actions.appendChild(MemoryUi.Button("Edit", function () { MemoryUi.Edit(row, item); }));
            actions.appendChild(MemoryUi.Button("Delete", function () { MemoryUi.MemoryAction("memory-delete", { id: row.memoryID }); }));
            item.appendChild(actions);

            MemoryUi.els.list.appendChild(item);
        }
    }

    static async MemoryAction(title, params) {
        await BackgroundMessaging.Message(title, params);
        MemoryUi.Refresh();
    }

    static Edit(row, item) {
        MemoryUi.editing = row;
        MemoryUi.MarkEditing(item);

        MemoryUi.els.formTitle.textContent = `Edit lesson #${row.memoryID}`;
        MemoryUi.els.saveTip.textContent = "Save changes";
        MemoryUi.SetEditVisible(true);

        const scope = MemoryUi.SCOPES.includes(row.scope) ? row.scope : MemoryUi.SCOPES[0];
        MemoryUi.els.tipScope.value = scope;
        MemoryUi.RenderTipFields();

        if (scope === "Ranking")
            MemoryUi.els.tipRankingKey.value = RANKING_KEYS.includes(row.fieldKey) ? row.fieldKey : RANKING_KEYS[0];
        else MemoryUi.els.tipFieldKey.value = row.fieldKey || "";

        MemoryUi.els.tipDomain.value = row.agencyDomain ?? "*";
        MemoryUi.els.tipFieldLabel.value = row.fieldLabel ?? "";
        MemoryUi.els.tipKind.value = MemoryUi.KINDS.includes(row.kind) ? row.kind : MemoryUi.KINDS[0];
        MemoryUi.els.tipConfirmed.checked = row.confirmed === true;
        MemoryUi.els.tipValue.value = row.value ?? "";
        MemoryUi.els.tipNote.value = row.note ?? "";

        if (MemoryUi.els.form.scrollIntoView) MemoryUi.els.form.scrollIntoView({ block: "nearest" });
    }

    static ResetTipForm() {
        MemoryUi.editing = null;
        MemoryUi.MarkEditing(null);

        MemoryUi.els.formTitle.textContent = "Add lesson (tip, confirmed)";
        MemoryUi.els.saveTip.textContent = "Save lesson";
        MemoryUi.SetEditVisible(false);

        MemoryUi.els.tipFieldKey.value = "";
        MemoryUi.els.tipValue.value = "";
        MemoryUi.els.tipNote.value = "";
        MemoryUi.RenderTipScope();
    }

    static SetEditVisible(visible) {
        const display = visible ? "" : "none";
        for (const element of [
            MemoryUi.els.tipDomainGroup,
            MemoryUi.els.tipFieldLabelGroup,
            MemoryUi.els.tipKindGroup,
            MemoryUi.els.tipConfirmedGroup,
            MemoryUi.els.tipCancel,
        ])
            element.style.display = display;
    }

    static MarkEditing(item) {
        const current = MemoryUi.els.list.querySelector(".memory-row.editing");
        if (current) current.classList.remove("editing");
        if (item) item.classList.add("editing");
    }

    static async SaveTip() {
        if (MemoryUi.editing) return MemoryUi.SaveEditForm();

        const scope = MemoryUi.els.tipScope.value;
        const fieldKey = scope === "Ranking"
            ? MemoryUi.els.tipRankingKey.value
            : FormInventory.NormalizeKey(MemoryUi.els.tipFieldKey.value, "");
        const value = MemoryUi.els.tipValue.value.trim();
        const note = MemoryUi.els.tipNote.value.trim();

        if (!fieldKey || !value) {
            MemoryUi.Status("lesson needs a field and a value");
            return;
        }

        const domain = scope === "Apply" && MemoryUi.hooks.domain ? MemoryUi.hooks.domain() : "*";
        const result = await BackgroundMessaging.Message("tip", {
            scope: scope,
            domain: domain ?? "*",
            fieldKey: fieldKey,
            fieldLabel: scope === "Ranking" ? undefined : FormInventory.CleanLabel(MemoryUi.els.tipFieldKey.value),
            value: value,
            note: note || undefined,
        });

        MemoryUi.Status(result?.error ? "lesson error: " + result.error : `saved ${scope} lesson`);
        MemoryUi.els.tipValue.value = "";
        MemoryUi.els.tipNote.value = "";
        if (MemoryUi.hooks.memoryLoaded && MemoryUi.hooks.memoryLoaded()) MemoryUi.Refresh();
    }

    static async SaveEditForm() {
        const scope = MemoryUi.els.tipScope.value;
        const ranking = scope === "Ranking";
        const patch = {
            scope: scope,
            domain: MemoryUi.els.tipDomain.value.trim(),
            fieldKey: ranking ? MemoryUi.els.tipRankingKey.value : FormInventory.NormalizeKey(MemoryUi.els.tipFieldKey.value, ""),
            fieldLabel: MemoryUi.els.tipFieldLabel.value.trim(),
            kind: MemoryUi.els.tipKind.value,
            confirmed: MemoryUi.els.tipConfirmed.checked,
            value: MemoryUi.els.tipValue.value.trim(),
            note: MemoryUi.els.tipNote.value.trim(),
        };

        if (!patch.fieldKey || !patch.value) {
            MemoryUi.Status("edit error: lesson needs a field and a value");
            return;
        }

        const result = await BackgroundMessaging.Message("memory-edit", {
            id: MemoryUi.editing.memoryID,
            row: patch,
        });

        if (result && result.error) {
            MemoryUi.Status("edit error: " + result.error);
            return;
        }

        MemoryUi.Status(`updated ${patch.scope} lesson`);
        MemoryUi.ResetTipForm();
        MemoryUi.Refresh();
    }

    static RenderTipScope() {
        MemoryUi.els.tipScope.options = MemoryUi.SCOPES.map(function (scope) {
            return { value: scope, text: scope.toLowerCase() };
        });
        MemoryUi.els.tipScope.value = MemoryUi.EffectiveMode() === "job_detail" ? "Ranking" : "Apply";
        MemoryUi.RenderTipFields();
    }

    static RenderTipFields() {
        const ranking = MemoryUi.els.tipScope.value === "Ranking";
        MemoryUi.els.tipRankingKey.style.display = ranking ? "" : "none";
        MemoryUi.els.tipFieldKey.style.display = ranking ? "none" : "";

        if (ranking && !MemoryUi.els.tipRankingKey.options.length)
            MemoryUi.els.tipRankingKey.options = RANKING_KEYS.map(key => ({ value: key, text: key }));
    }

    static async LoadChatLog() {
        const log = (await StorageHandler.GetSession(StorageHandler.CHAT_LOG, [])) || [];
        MemoryUi.Status(log.length
            ? "recent lessons: " + log.map(entry => `${entry.scope}:${entry.fieldKey}`).join(", ")
            : "no lessons this session");
    }

    static Button(text, onClick) {
        const button = document.createElement("button");
        button.className = "btn btn-outline-primary";
        button.textContent = text;
        button.addEventListener("click", onClick);
        return button;
    }

    static Status(text) {
        if (MemoryUi.els.chatLog) MemoryUi.els.chatLog.textContent = text;
    }
}
