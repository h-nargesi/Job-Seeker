console.log("background-messaging");

class BackgroundMessaging {

    static MESSAGE_ID = 0;
    static CURRENT_REQUESTS;

    static RunListener() {
        if (BackgroundMessaging.CURRENT_REQUESTS) return;

        BackgroundMessaging.CURRENT_REQUESTS = {};
        chrome.runtime.onMessage.addListener(
            function (response) {
                if (chrome.runtime.lastError)
                    console.error(chrome.runtime.lastError.message);

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
            BackgroundMessaging.CURRENT_REQUESTS[message.id] = resolve;
            chrome.runtime.sendMessage(message);
        });
    }

    static async Send(params) {
        return BackgroundMessaging.Message({ title: "send", params });
    }

    static async Scopes(reset) {
        return BackgroundMessaging.Message({ title: "scopes", params: { reset } });
    }
}
