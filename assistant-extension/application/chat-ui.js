console.log("ASSISTANT", "chat-ui");

class ChatUi {

    static MAX_ENTRIES = 100;

    static els = {};
    static transcript = [];
    static hooks = { mode: null, domain: null };
    static sending = false;

    static Bind() {
        ChatUi.els = {
            area: document.getElementById("ChatLogArea"),
            entries: document.getElementById("ChatEntries"),
            input: document.getElementById("ChatInput"),
            send: document.getElementById("ChatSend"),
        };
    }

    static async Init(hooks) {
        ChatUi.Bind();
        if (hooks) ChatUi.hooks = hooks;

        ChatUi.els.send.addEventListener("click", function () { ChatUi.Send(); });
        ChatUi.els.input.addEventListener("keydown", function (event) {
            if (event.key !== "Enter" || event.shiftKey) return;
            event.preventDefault();
            ChatUi.Send();
        });

        ChatUi.transcript = (await StorageHandler.GetSession(StorageHandler.CHAT_TRANSCRIPT, [])) || [];
        ChatUi.Render();
    }

    static async Send() {
        const text = ChatUi.els.input.value.trim();
        if (!text || ChatUi.sending) return;

        const tabId = await ChatUi.ActiveTabId();
        if (!tabId) {
            ChatUi.Report("no active tab");
            return;
        }

        const mode = ChatUi.hooks.mode ? ChatUi.hooks.mode() : "apply_form";
        const domain = ChatUi.hooks.domain ? ChatUi.hooks.domain() : "*";

        ChatUi.els.input.value = "";
        await ChatUi.Append({ kind: "user", text: text });
        ChatUi.SetBusy(true);

        const result = await BackgroundMessaging.Message("lesson-extract", {
            text: text,
            tabId: tabId,
            mode: mode,
            domain: domain,
        }, BackgroundMessaging.LONG_TIMEOUT);

        ChatUi.SetBusy(false);

        if (!result || result.error) {
            ChatUi.Append({ kind: "error", text: "assistant error: " + ((result && result.error) || "unknown") });
            return;
        }

        await ChatUi.Append({ kind: "assistant", text: result.reply || "(no reply)" });

        for (const drop of result.dropped || []) {
            await ChatUi.Report("dropped candidate (" + drop.scope + "/" + (drop.fieldKey || "-") + "): " + drop.reason);
        }

        for (const candidate of result.candidates || []) {
            await ChatUi.Append({ kind: "card", card: candidate });
        }
    }

    static SetBusy(busy) {
        ChatUi.sending = busy;
        ChatUi.els.input.disabled = busy;
        ChatUi.els.send.disabled = busy;
        ChatUi.els.input.placeholder = busy ? "assistant is thinking ..." : "teach a lesson ...";
    }

    static async Report(text) {
        if (!text) return;
        await ChatUi.Append({ kind: "report", text: String(text) });
    }

    static async Append(entry) {
        ChatUi.transcript.push(entry);
        const trimmed = ChatUi.transcript.length > ChatUi.MAX_ENTRIES;
        if (trimmed) ChatUi.transcript = ChatUi.transcript.slice(-ChatUi.MAX_ENTRIES);
        await ChatUi.Save();

        if (!ChatUi.els.entries) return;
        if (trimmed) {
            ChatUi.Render();
            return;
        }
        ChatUi.els.entries.appendChild(ChatUi.Entry(entry));
        ChatUi.Scroll();
    }

    static async Save() {
        await StorageHandler.SetSession(StorageHandler.CHAT_TRANSCRIPT, ChatUi.transcript);
    }

    static Render() {
        if (!ChatUi.els.entries) return;

        ChatUi.els.entries.innerHTML = "";
        for (const entry of ChatUi.transcript) ChatUi.els.entries.appendChild(ChatUi.Entry(entry));
        ChatUi.Scroll();
    }

    static Scroll() {
        if (ChatUi.els.area) ChatUi.els.area.scrollTop = ChatUi.els.area.scrollHeight;
    }

    static Entry(entry) {
        const node = document.createElement("div");

        if (entry.kind === "card") {
            node.className = "chat-entry chat-card";
            ChatUi.Card(entry, node);
            return node;
        }

        node.className = "chat-entry" + (entry.kind === "user" ? " fw-semibold" : "");
        if (entry.kind === "report") node.classList.add("muted");
        if (entry.kind === "error") node.classList.add("warn");
        node.textContent = entry.text ?? "";
        return node;
    }

    static Scopes() {
        return ["Apply", "Ranking", "Resume"];
    }

    static Card(entry, node) {
        const card = entry.card;

        if (card.resolved) {
            node.classList.add("muted");
            node.textContent = card.resolved + " lesson — " + card.scope + "/" + card.fieldKey
                + " = " + String(card.value).slice(0, 80);
            return;
        }

        const scopes = ChatUi.Scopes();
        if (!scopes.includes(card.scope)) card.scope = scopes[0];

        const head = document.createElement("div");
        head.className = "muted";
        head.textContent = "proposed lesson — review, edit, then accept";
        node.appendChild(head);

        const scopeWrap = document.createElement("label");
        scopeWrap.className = "mb-1 d-block";
        scopeWrap.textContent = "Scope: ";
        node.appendChild(scopeWrap);

        const scope = ChatUi.Picker(scopes.map(function (scope) {
            return { value: scope, text: scope.toLowerCase() };
        }));
        scope.value = card.scope;
        scopeWrap.appendChild(scope);

        const keyWrap = document.createElement("label");
        keyWrap.className = "mb-1 d-block";
        keyWrap.textContent = "Field: ";
        node.appendChild(keyWrap);

        const keyHolder = document.createElement("div");
        keyWrap.appendChild(keyHolder);

        const rankingPick = ChatUi.Picker(RANKING_KEYS.map(function (key) {
            return { value: key, text: key };
        }));
        const keyInput = document.createElement("input");
        keyInput.type = "text";
        keyInput.className = "form-control form-control";
        keyInput.placeholder = "field key (name or label)";

        function RenderKey() {
            keyHolder.innerHTML = "";
            if (scope.value === "Ranking") {
                rankingPick.value = RANKING_KEYS.includes(card.fieldKey) ? card.fieldKey : RANKING_KEYS[0];
                keyHolder.appendChild(rankingPick);
            }
            else {
                keyInput.value = card.fieldKey || "";
                keyHolder.appendChild(keyInput);
            }
        }

        scope.addEventListener("change", RenderKey);
        RenderKey();

        const valueInput = ChatUi.Field("value", card.value ?? "");
        node.appendChild(valueInput.wrap);
        const noteInput = ChatUi.Field("note", card.note ?? "");
        node.appendChild(noteInput.wrap);

        const actions = document.createElement("div");
        actions.appendChild(ChatUi.Button("Accept", function () {
            ChatUi.Accept(entry, function () {
                return {
                    scope: scope.value,
                    fieldKey: scope.value === "Ranking"
                        ? rankingPick.value
                        : FormInventory.NormalizeKey(keyInput.value, ""),
                    fieldLabel: scope.value === "Ranking" ? undefined : FormInventory.CleanLabel(keyInput.value),
                    value: valueInput.input.value.trim(),
                    note: noteInput.input.value.trim() || undefined,
                };
            });
        }));
        actions.appendChild(ChatUi.Button("Reject", function () { ChatUi.Reject(entry); }));
        node.appendChild(actions);
    }

    static Picker(options) {
        const wrap = document.createElement("div");
        wrap.className = "dropdown";

        const toggle = document.createElement("button");
        toggle.type = "button";
        toggle.className = "form-select form-select text-start";
        wrap.appendChild(toggle);

        const menu = document.createElement("div");
        menu.className = "dropdown-menu w-100";
        menu.style.maxHeight = "240px";
        menu.style.overflowY = "auto";
        wrap.appendChild(menu);

        Dropdown.Attach(wrap);
        wrap.options = options;
        return wrap;
    }

    static Field(label, value) {
        const wrap = document.createElement("label");
        wrap.className = "mb-1 d-block";
        wrap.textContent = label + ": ";

        const input = document.createElement("input");
        input.type = "text";
        input.className = "form-control form-control";
        input.value = value;
        wrap.appendChild(input);
        return { wrap: wrap, input: input };
    }

    static Button(text, onClick) {
        const button = document.createElement("button");
        button.className = "btn btn-outline-primary";
        button.textContent = text;
        button.addEventListener("click", onClick);
        return button;
    }

    static async Accept(entry, collect) {
        const current = collect();
        if (!current.fieldKey || !current.value) {
            ChatUi.Report("lesson needs a field and a value");
            return;
        }

        const domain = current.scope === "Apply" ? (entry.card.domain || "*") : "*";
        const result = await BackgroundMessaging.Message("tip", {
            scope: current.scope,
            domain: domain,
            fieldKey: current.fieldKey,
            fieldLabel: current.fieldLabel,
            value: current.value,
            note: current.note,
        });

        if (result && result.error) {
            ChatUi.Report("lesson error: " + result.error);
            return;
        }

        Object.assign(entry.card, current, { domain: domain, resolved: "accepted" });
        await ChatUi.Save();
        ChatUi.Render();
        await ChatUi.Report("saved " + current.scope + " lesson");
    }

    static async Reject(entry) {
        entry.card.resolved = "rejected";
        await ChatUi.Save();
        ChatUi.Render();
        await ChatUi.Report("lesson candidate discarded");
    }

    static async ActiveTabId() {
        try {
            const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
            if (tabs && tabs.length) return tabs[0].id;
        } catch (e) {
            console.log("ASSISTANT", "ChatUi", e);
        }
        return null;
    }
}
