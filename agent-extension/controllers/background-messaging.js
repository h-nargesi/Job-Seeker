console.log("AGENT", "background-messaging");

class BackgroundMessaging {

    static MESSAGE_ID = 0;
    static CURRENT_REQUESTS;
    static RESPONSE_TIMEOUT = 45000;

    static RunListener() {
        if (BackgroundMessaging.CURRENT_REQUESTS) return;

        BackgroundMessaging.CURRENT_REQUESTS = {};
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
            respond(response);
        }
    }

    static async Message(message) {
        BackgroundMessaging.RunListener();
        return new Promise(function (resolve) {
            message.id = ++BackgroundMessaging.MESSAGE_ID;
            try {
                BackgroundMessaging.CURRENT_REQUESTS[message.id] = resolve;
                chrome.runtime.sendMessage(message);

                setTimeout(function () {
                    BackgroundMessaging.CheckRequests(message.id, { error: "no-response", status: 0 });
                }, BackgroundMessaging.RESPONSE_TIMEOUT);
            } catch (e) {
                delete BackgroundMessaging.CURRENT_REQUESTS[message.id];
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
}
