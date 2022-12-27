console.log("trend-collection");

class TrendCollection {

    TRENDS = {}

    get(window, tab) {
        if (window == undefined || tab == undefined)
            throw "window or tab are undefined";

        if (this.TRENDS.hasOwnProperty(window)) {
            const tabs = this.TRENDS[window];
            if (tabs.hasOwnProperty(tab)) {
                return tabs[tab];
            }
        }

        return null;
    }

    set(window, tab, trend) {
        if (window == undefined || tab == undefined)
            throw "window or tab are undefined";

        let tabs = null;

        if (!this.TRENDS.hasOwnProperty(window)) {
            this.TRENDS[window] = tabs = {};
        } else {
            tabs = this.TRENDS[window];
        }

        tabs[tab] = trend;
    }
}