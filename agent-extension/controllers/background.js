console.log("background");

importScripts("./core-messaging.js", "./storage-handler.js", "./trend-collection.js");

const messaging = new CoreMessaging();
const trends = new TrendCollection();

chrome.runtime.onMessage.addListener(
    function (request, sender) {
        // console.log("Background", request, sender.tab);

        switch (request.title.toLowerCase()) {
            case "send":
                request.params["trend"] = trends.get(sender.tab.windowId, sender.tab.index);
                Respond(sender.tab, request.id, messaging.Send(request.params));
                break;
            case "scopes":
                Respond(sender.tab, request.id, messaging.Scopes(request.params.reset));
                break;
            case "orders":
                Respond(sender.tab, request.id, messaging.Orders());
                break;
            case "reset":
                Respond(sender.tab, request.id, messaging.Reset());
                break;
        }
    }
);

async function Respond(tab, id, promise) {
    let response = await promise;

    if (response.trend !== undefined) {
        if (response.trend)
            trends.set(tab.windowId, tab.index, response.trend);
        response = response.commands;
    }

    chrome.tabs.query({ windowId: tab.windowId, index: tab.index }, function (tabs) {
        chrome.tabs.sendMessage(tabs[0].id, { id, body: response });
    });
}