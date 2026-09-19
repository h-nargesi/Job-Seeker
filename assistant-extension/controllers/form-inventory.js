console.log("ASSISTANT", "form-inventory");

class FormInventory {

    static Registry = new Map();

    static SKIPPED_INPUT_TYPES = ["hidden", "button", "submit", "reset", "image"];
    static MAX_OPTIONS = 80;
    static MAX_LABEL = 120;

    static Extract() {
        FormInventory.Registry = new Map();
        const groups = new Map();

        document.querySelectorAll("input, select, textarea").forEach(function (element) {
            if (element.disabled || element.type === "hidden") return;
            if (element.tagName === "INPUT" && FormInventory.SKIPPED_INPUT_TYPES.includes(element.type)) return;

            const entry = FormInventory.Entry(element);
            if (!entry) return;

            if (entry.type === "radio" && entry.name) {
                const group = groups.get(entry.name);
                if (group) {
                    group.options.push({ value: element.value, text: FormInventory.OwnLabel(element) });
                    return;
                }
                entry.options = [{ value: element.value, text: FormInventory.CleanLabel(FormInventory.OwnLabel(element)) }];
                groups.set(entry.name, entry);
            }

            FormInventory.Register(entry, element);
        });

        const inventory = [];
        FormInventory.Registry.forEach(function (record) {
            inventory.push(record.entry);
        });
        return inventory;
    }

    static Register(entry, element) {
        entry.fieldId = "f" + (FormInventory.Registry.size + 1);
        FormInventory.Registry.set(entry.fieldId, { element: element, entry: entry });
    }

    static Entry(element) {
        const tag = element.tagName.toLowerCase();
        const type = tag === "input" ? (element.type || "text") : tag;
        const name = element.name || "";
        const label = type === "radio"
            ? (FormInventory.GroupLabel(element) || FormInventory.OwnLabel(element))
            : FormInventory.OwnLabel(element);
        const clean = FormInventory.CleanLabel(label);

        const entry = {
            tag: tag,
            type: type,
            name: name,
            label: clean,
            fieldKey: FormInventory.NormalizeKey(name, label),
            required: element.required === true,
        };

        if (tag === "textarea") entry.longText = true;
        else if (tag === "select") entry.options = FormInventory.Options(element);
        else if (type === "file") entry.manual = true;
        else if (type === "checkbox") {
            entry.options = [
                { value: "true", text: "checked" },
                { value: "false", text: "unchecked" },
            ];
        }

        return entry;
    }

    static Options(select) {
        const options = [];
        const nodes = select.querySelectorAll("option");
        for (let i = 0; i < nodes.length && options.length < FormInventory.MAX_OPTIONS; i++) {
            const text = FormInventory.CleanLabel(nodes[i].textContent);
            if (!text && !nodes[i].value) continue;
            options.push({ value: nodes[i].value, text: text });
        }
        return options;
    }

    static OwnLabel(element) {
        if (element.labels && element.labels.length) return element.labels[0].textContent;

        if (element.id) {
            const external = document.querySelector('label[for="' + element.id + '"]');
            if (external) return external.textContent;
        }

        const wrapping = element.closest ? element.closest("label") : null;
        if (wrapping) return wrapping.textContent;

        return element.getAttribute("aria-label")
            || element.getAttribute("placeholder")
            || element.getAttribute("title")
            || "";
    }

    static GroupLabel(element) {
        if (!element.closest) return "";
        const fieldset = element.closest("fieldset");
        if (!fieldset) return "";
        const legend = fieldset.querySelector("legend");
        return legend ? legend.textContent : "";
    }

    static CleanLabel(text) {
        const clean = String(text ?? "").replace(/\s+/g, " ").trim();
        return clean.length > FormInventory.MAX_LABEL ? clean.slice(0, FormInventory.MAX_LABEL) : clean;
    }

    static NormalizeKey(name, label) {
        const raw = String(name || label || "");
        const key = raw.toLowerCase().replace(/\s+/g, " ").trim().replace(/[:*]+$/, "").trim();
        return key.slice(0, 200);
    }
}
