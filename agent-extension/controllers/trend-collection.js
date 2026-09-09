console.log("AGENT", "trend-collection");

class TrendCollection {

    static STORAGE_KEY = "tab-trends";

    TRENDS = {}
    ready = null

    constructor() {
        this.ready = this.restore();
    }

    async restore() {
        try {
            const items = await chrome.storage.session.get(TrendCollection.STORAGE_KEY);
            const stored = items && items[TrendCollection.STORAGE_KEY];

            if (stored && typeof stored === "object") {
                this.TRENDS = stored;
                console.log("AGENT", "TrendCollection", "restored", stored);
            }
        } catch (e) {
            console.error("AGENT", "TrendCollection", "restore", e);
        }
    }

    async persist() {
        try {
            await chrome.storage.session.set({ [TrendCollection.STORAGE_KEY]: this.TRENDS });
        } catch (e) {
            console.error("AGENT", "TrendCollection", "persist", e);
        }
    }

    async get(tab) {
        if (tab == undefined) throw "tab is undefined";

        await this.ready;

        return this.TRENDS.hasOwnProperty(tab) ? this.TRENDS[tab] : null;
    }

    async set(tab, trend) {
        if (tab == undefined) throw "tab is undefined";

        await this.ready;

        this.TRENDS[tab] = trend;
        await this.persist();
    }

    async remove(tab) {
        if (tab == undefined) throw "tab is undefined";

        await this.ready;

        if (this.TRENDS.hasOwnProperty(tab)) {
            delete this.TRENDS[tab];
            await this.persist();
        }
    }
}
