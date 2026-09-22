console.log("AGENT", "background-messaging");

class BackgroundMessaging {

    static MESSAGE_ID = 0;
    static CURRENT_REQUESTS;
    static CURRENT_TIMERS;
    static RESPONSE_TIMEOUT = 45000;

    static RunListener() {
        if (BackgroundMessaging.CURRENT_REQUESTS) return;

        BackgroundMessaging.CURRENT_REQUESTS = {};
        BackgroundMessaging.CURRENT_TIMERS = {};
        chrome.runtime.onMessage.addListener(
            function (response) {
                if (chrome.runtime.lastError)
                    console.log("AGENT", chrome.runtime.lastError.message);

                if (!response.id || !response.body) return;

                BackgroundMessaging.CheckRequests(response.id, response.body);
            }
        );
    }

    static CheckRequests(id, response) {
        if (id in BackgroundMessaging.CURRENT_REQUESTS) {
            const respond = BackgroundMessaging.CURRENT_REQUESTS[id];
            delete BackgroundMessaging.CURRENT_REQUESTS[id];

            const timer = BackgroundMessaging.CURRENT_TIMERS[id];
            if (timer !== undefined) {
                clearTimeout(timer);
                delete BackgroundMessaging.CURRENT_TIMERS[id];
            }

            respond(response);
        }
    }

    static async Message(message) {
        BackgroundMessaging.RunListener();
        return new Promise(function (resolve) {
            message.id = ++BackgroundMessaging.MESSAGE_ID;
            const id = message.id;
            try {
                BackgroundMessaging.CURRENT_REQUESTS[id] = resolve;
                chrome.runtime.sendMessage(message);

                if (!(id in BackgroundMessaging.CURRENT_REQUESTS)) return;

                BackgroundMessaging.CURRENT_TIMERS[id] = setTimeout(function () {
                    delete BackgroundMessaging.CURRENT_TIMERS[id];
                    BackgroundMessaging.CheckRequests(id, { error: "no-response", status: 0 });
                }, BackgroundMessaging.RESPONSE_TIMEOUT);
            } catch (e) {
                delete BackgroundMessaging.CURRENT_REQUESTS[id];
                console.error("AGENT", "BackgroundMessaging", e);
                resolve({ error: "no-response", status: 0 });
            }
        });
    }

    static async Send(params) {
        return BackgroundMessaging.Message({ title: "send", params });
    }

    static async Scopes(reset) {
        return BackgroundMessaging.Message({ title: "scopes", params: { reset } });
    }

    static async Orders() {
        return BackgroundMessaging.Message({ title: "orders" });
    }

    static async Heartbeat() {
        return BackgroundMessaging.Message({ title: "heartbeat" });
    }

    static async OpenTab(url) {
        return BackgroundMessaging.Message({ title: "open-tab", params: { url } });
    }

    static async CloseTab() {
        return BackgroundMessaging.Message({ title: "close-tab" });
    }
}
