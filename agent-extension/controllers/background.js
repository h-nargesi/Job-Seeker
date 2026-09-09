console.log("AGENT", "background");

importScripts("./core-messaging.js", "./storage-handler.js", "./trend-collection.js");

const messaging = new CoreMessaging();
const trends = new TrendCollection();

chrome.runtime.onMessage.addListener(
    async function (request, sender) {
        // console.log("AGENT", "Background", request, sender.tab.windowId, sender.tab.id);

        if (!sender.tab) {
            console.error("AGENT", "Background", "no tab in sender", request);
            return;
        }

        switch (request.title.toLowerCase()) {
            case "send":
                request.params["trend"] = await trends.get(sender.tab.id);
                Respond(sender.tab, request.id, messaging.Send(request.params));
                break;
            case "scopes":
                Respond(sender.tab, request.id, messaging.Scopes(request.params.reset));
                break;
            case "orders":
                Respond(sender.tab, request.id, messaging.Orders());
                break;
            case "heartbeat":
                request.params = { trend: await trends.get(sender.tab.id) };
                Respond(sender.tab, request.id, messaging.Heartbeat(request.params));
                break;
            case "open-tab":
                Respond(sender.tab, request.id, OpenTab(request.params?.url));
                break;
            case "close-tab":
                Respond(sender.tab, request.id, CloseTab(sender.tab.id));
                break;
        }
    }
);

chrome.tabs.onRemoved.addListener(function (tabId) {
    trends.remove(tabId);
});

async function OpenTab(url) {
    if (!url) return { error: "open-failed" };

    try {
        await chrome.tabs.create({ url: url, active: false });
        return { ok: true };
    } catch (e) {
        console.error("AGENT", "OpenTab", e);
        return { error: "open-failed" };
    }
}

async function CloseTab(tabId) {
    try {
        await chrome.tabs.remove(tabId);
        return { ok: true };
    } catch (e) {
        console.error("AGENT", "CloseTab", e);
        return { error: "close-failed" };
    }
}

async function Respond(tab, id, promise) {
    let response;

    try {
        response = await promise;
    } catch (e) {
        console.error("AGENT", "Respond", e);
        response = { error: "background", status: 0 };
    }

    if (response && response.error === undefined && response.trend !== undefined && response.commands !== undefined) {
        if (response.trend) await trends.set(tab.id, response.trend);
        else await trends.remove(tab.id);

        response = { commands: response.commands, close_timeout_ms: response.close_timeout_ms };
    }

    chrome.tabs.sendMessage(tab.id, { id, body: response }, function () {
        if (chrome.runtime.lastError)
            console.log("AGENT", "Respond", chrome.runtime.lastError);
    });
}
