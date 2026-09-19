console.log("ASSISTANT", "background-messaging");

class BackgroundMessaging {

    static RESPONSE_TIMEOUT = 120000;

    static async Message(title, params) {
        const request = { title: title };
        if (params !== undefined) request.params = params;

        return new Promise(function (resolve) {
            let settled = false;

            const finish = function (error_name, response) {
                if (settled) return;
                settled = true;
                clearTimeout(timer);
                resolve(error_name
                    ? { error: error_name, status: 0 }
                    : (response ?? { error: "no-response", status: 0 }));
            };

            const timer = setTimeout(function () { finish("timeout"); }, BackgroundMessaging.RESPONSE_TIMEOUT);

            try {
                chrome.runtime.sendMessage(request, function (response) {
                    if (chrome.runtime.lastError) {
                        console.log("ASSISTANT", "BackgroundMessaging", chrome.runtime.lastError.message);
                        finish("no-response");
                        return;
                    }
                    finish(null, response);
                });
            } catch (e) {
                console.error("ASSISTANT", "BackgroundMessaging", e);
                finish("no-response");
            }
        });
    }
}
