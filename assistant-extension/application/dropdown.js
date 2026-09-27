console.log("ASSISTANT", "dropdown.js");

class Dropdown {

    static bound = false;
    static live = [];

    static Attach(element) {
        if (!element || element.__dropdown) return element;

        const toggle = element.querySelector(".form-select");
        const menu = element.querySelector(".dropdown-menu");
        if (!toggle || !menu) return element;

        element.__dropdown = new Dropdown(element, toggle, menu);
        Dropdown.live.push(element.__dropdown);
        Dropdown.BindDocument();
        return element;
    }

    static BindDocument() {
        if (Dropdown.bound) return;
        Dropdown.bound = true;

        document.addEventListener("click", function (event) {
            for (const control of Dropdown.live)
                if (!control.element.contains(event.target)) control.close();
        });

        document.addEventListener("keydown", function (event) {
            if (event.key !== "Escape") return;
            for (const control of Dropdown.live) control.close();
        });
    }

    constructor(element, toggle, menu) {
        this.element = element;
        this.toggle = toggle;
        this.menu = menu;
        this.current = "";

        const self = this;

        this.toggle.addEventListener("click", function (event) {
            event.stopPropagation();
            self.menu.classList.toggle("show");
        });

        this.menu.addEventListener("click", function (event) {
            const target = event.target;
            if (!target || !target.closest) return;

            const item = target.closest(".dropdown-item");
            if (!item) return;

            self.set(item.dataset.value ?? "");
            self.close();
            self.element.dispatchEvent(new Event("change", { bubbles: true }));
        });

        Object.defineProperty(element, "value", {
            get() { return self.current; },
            set(value) { self.set(String(value ?? "")); },
        });

        Object.defineProperty(element, "options", {
            get() { return self.items().map(item => ({ value: item.dataset.value ?? "", text: item.textContent })); },
            set(list) { self.rebuild(Array.isArray(list) ? list : []); },
        });

        const first = this.items()[0];
        this.current = first ? first.dataset.value ?? "" : "";
        this.sync();
    }

    items() {
        return Array.from(this.menu.querySelectorAll(".dropdown-item"));
    }

    set(value) {
        this.current = String(value ?? "");
        this.sync();
    }

    rebuild(list) {
        this.menu.innerHTML = "";

        for (const entry of list) {
            const item = document.createElement("button");
            item.type = "button";
            item.className = "dropdown-item";
            item.dataset.value = String(entry?.value ?? "");
            item.textContent = entry?.text ?? "";
            this.menu.appendChild(item);
        }

        this.current = list.length ? String(list[0]?.value ?? "") : "";
        this.sync();
    }

    sync() {
        let label = "";
        for (const item of this.items()) {
            const active = (item.dataset.value ?? "") === this.current;
            item.classList.toggle("active", active);
            if (active) label = item.textContent;
        }
        this.toggle.textContent = label || this.current;
    }

    close() {
        this.menu.classList.remove("show");
    }
}
