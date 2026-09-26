console.log("ASSISTANT", "logger");

class AssistantLog {

    static CAP = 500;
    static DETAIL_MAX = 200;
    static WHERE_MAX = 60;

    static async Write(level, where, detail) {
        const entry = {
            t: Date.now(),
            level: AssistantLog.Level(level),
            where: AssistantLog.Text(where).slice(0, AssistantLog.WHERE_MAX),
            detail: AssistantLog.Text(detail).slice(0, AssistantLog.DETAIL_MAX),
        };

        try {
            const buffer = (await StorageHandler.Get(StorageHandler.LOG_BUFFER, [])) || [];
            buffer.push(entry);
            await StorageHandler.Set(StorageHandler.LOG_BUFFER, buffer.slice(-AssistantLog.CAP));
        } catch (e) {
            console.error("ASSISTANT", "AssistantLog", e);
        }
    }

    static Level(level) {
        return level === "warn" || level === "error" ? level : "info";
    }

    static Text(value) {
        if (value === null || value === undefined) return "";
        if (typeof value === "string") return value;
        try {
            return JSON.stringify(value);
        } catch (e) {
            return String(value);
        }
    }

    static async All() {
        return (await StorageHandler.Get(StorageHandler.LOG_BUFFER, [])) || [];
    }

    static async Clear() {
        await StorageHandler.Set(StorageHandler.LOG_BUFFER, []);
    }
}
