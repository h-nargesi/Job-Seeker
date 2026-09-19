console.log("ASSISTANT", "fill-handler");

class FillHandler {

    static Filled = new Map();

    static Apply(fieldId, value) {
        const record = FormInventory.Registry.get(fieldId);
        if (!record) return { ok: false, error: "unknown-field" };

        const entry = record.entry;
        if (entry.manual) return { ok: false, error: "manual" };

        const result = entry.type === "radio"
            ? FillHandler.ApplyRadio(entry, String(value))
            : FillHandler.ApplyControl(entry, record.element, value);

        if (!result.ok) return result;

        FillHandler.Filled.set(fieldId, { value: String(value), entry: entry });
        return { ok: true };
    }

    static ApplyRadio(entry, value) {
        const nodes = document.querySelectorAll('input[type="radio"][name="' + entry.name + '"]');
        for (let i = 0; i < nodes.length; i++) {
            if (nodes[i].value === value) {
                nodes[i].checked = true;
                nodes[i].dispatchEvent(new Event("input", { bubbles: true }));
                nodes[i].dispatchEvent(new Event("change", { bubbles: true }));
                return { ok: true };
            }
        }
        return { ok: false, error: "no-option" };
    }

    static ApplyControl(entry, element, value) {
        if (entry.type === "checkbox") {
            const checked = ["true", "yes", "1", "on", "checked"].includes(String(value).toLowerCase());
            element.checked = checked;
            element.dispatchEvent(new Event("input", { bubbles: true }));
            element.dispatchEvent(new Event("change", { bubbles: true }));
            return { ok: true };
        }

        if (entry.tag === "select") return FillHandler.ApplySelect(entry, element, String(value));

        FillHandler.SetValue(element, String(value));
        return { ok: true };
    }

    static ApplySelect(entry, element, value) {
        const wanted = String(value).toLowerCase();
        const options = entry.options || [];
        for (let i = 0; i < options.length; i++) {
            if (options[i].value === wanted || options[i].value.toLowerCase() === wanted) {
                FillHandler.SetValue(element, options[i].value);
                return { ok: true };
            }
        }
        for (let i = 0; i < options.length; i++) {
            if (options[i].text.toLowerCase() === wanted) {
                FillHandler.SetValue(element, options[i].value);
                return { ok: true };
            }
        }
        return { ok: false, error: "no-option" };
    }

    static SetValue(element, value) {
        const proto = element.tagName === "TEXTAREA"
            ? HTMLTextAreaElement.prototype
            : element.tagName === "SELECT" ? HTMLSelectElement.prototype : HTMLInputElement.prototype;

        const descriptor = Object.getOwnPropertyDescriptor(proto, "value");
        if (descriptor && descriptor.set) descriptor.set.call(element, value);
        else element.value = value;

        element.dispatchEvent(new Event("input", { bubbles: true }));
        element.dispatchEvent(new Event("change", { bubbles: true }));
    }

    static Current(fieldId) {
        const record = FormInventory.Registry.get(fieldId);
        if (!record) return null;

        const entry = record.entry;
        if (entry.type === "radio") {
            const nodes = document.querySelectorAll('input[type="radio"][name="' + entry.name + '"]');
            for (let i = 0; i < nodes.length; i++) if (nodes[i].checked) return nodes[i].value;
            return "";
        }
        if (entry.type === "checkbox") return record.element.checked ? "true" : "false";
        return String(record.element.value ?? "");
    }

    static Diffs() {
        const diffs = [];
        FillHandler.Filled.forEach(function (filled, fieldId) {
            const current = FillHandler.Current(fieldId);
            if (current === null || current === filled.value) return;
            if (!current && !filled.value) return;

            diffs.push({
                fieldKey: filled.entry.fieldKey,
                fieldLabel: filled.entry.label,
                aiValue: filled.value,
                finalValue: current,
            });
        });
        return diffs;
    }

    static Reset() {
        FillHandler.Filled = new Map();
    }
}
