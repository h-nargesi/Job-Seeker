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
        }
    }
);

chrome.tabs.onRemoved.addListener(function (tabId) {
    trends.remove(tabId);
});

async function Respond(tab, id, promise) {
    let response;

    try {
        response = await promise;
    } catch (e) {
        console.error("AGENT", "Respond", e);
        response = { error: "background", status: 0 };
    }

    if (response && response.error === undefined && response.trend !== undefined) {
        if (response.trend) await trends.set(tab.id, response.trend);
        else await trends.remove(tab.id);

        response = response.commands;
    }

    chrome.tabs.sendMessage(tab.id, { id, body: response }, function () {
        if (chrome.runtime.lastError)
            console.log("AGENT", "Respond", chrome.runtime.lastError);
    });
}
