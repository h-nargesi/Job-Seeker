console.log("ASSISTANT", "memory-tools");

class MemoryTools {

    static Snapshot(messaging) {
        let cached = null;

        return function () {
            if (!cached) {
                cached = messaging.MemoryList("Apply", true).then(
                    function (rows) {
                        if (!Array.isArray(rows)) cached = null;
                        return rows;
                    },
                    function () {
                        cached = null;
                        return { error: "memory-query-failed" };
                    },
                );
            }
            return cached;
        };
    }

    static async Query(messaging, domain, fieldKey, load) {
        const rows = await (load ? load() : messaging.MemoryList("Apply", true));
        if (!Array.isArray(rows)) return { error: rows.error ?? "memory-query-failed", rows: [] };

        const key = String(fieldKey).toLowerCase();
        const matches = rows.filter(function (row) {
            if (String(row.fieldKey ?? "").toLowerCase() !== key) return false;
            return row.agencyDomain === domain || row.agencyDomain === "*";
        });

        matches.sort(MemoryTools.Precedence);
        return { rows: matches };
    }

    static Precedence(a, b) {
        const kind = a.kind === "Correction" ? 0 : 1;
        const other = b.kind === "Correction" ? 0 : 1;
        if (kind !== other) return kind - other;

        const domain = a.agencyDomain === "*" ? 1 : 0;
        const other_domain = b.agencyDomain === "*" ? 1 : 0;
        if (domain !== other_domain) return domain - other_domain;

        if ((b.useCount ?? 0) !== (a.useCount ?? 0)) return (b.useCount ?? 0) - (a.useCount ?? 0);
        return String(b.updatedAt ?? "").localeCompare(String(a.updatedAt ?? ""));
    }

    static async SaveApplyFact(messaging, domain, fieldKey, fieldLabel, value, note) {
        return messaging.MemorySave({
            scope: "Apply",
            domain: domain,
            fieldKey: fieldKey,
            fieldLabel: fieldLabel || undefined,
            kind: "Tip",
            confirmed: false,
            value: value,
            note: note || undefined,
        });
    }

    static async SaveCorrection(messaging, diff) {
        return messaging.MemorySave({
            scope: "Apply",
            domain: diff.domain,
            fieldKey: diff.fieldKey,
            fieldLabel: diff.fieldLabel || undefined,
            kind: "Correction",
            confirmed: false,
            value: diff.finalValue,
            note: "ai filled: " + diff.aiValue,
        });
    }

    static async SaveTip(messaging, tip) {
        return messaging.MemorySave({
            scope: tip.scope,
            domain: tip.domain,
            fieldKey: tip.fieldKey,
            fieldLabel: tip.fieldLabel || undefined,
            kind: "Tip",
            confirmed: true,
            value: tip.value,
            note: tip.note || undefined,
        });
    }
}
