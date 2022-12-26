console.log("core-messaging");

class CoreMessaging {

    static SERVER_URL;
    static SCOPES;
    static HEADERS = {
        'Accept': 'application/json',
        'Content-Type': 'application/json'
    };

    async CheckServerUrl() {
        if (CoreMessaging.SERVER_URL === undefined) {
            CoreMessaging.SERVER_URL = await StorageHandler.ServerUrlAsync();
            if (!CoreMessaging.SERVER_URL.endsWith('/')) CoreMessaging.SERVER_URL += '/';
        }

        return CoreMessaging.SERVER_URL;
    }

    async Send(params) {

        console.log(params);

        const server_url = await this.CheckServerUrl() + "decision/take";

        const data = {
            method: 'POST',
            headers: CoreMessaging.HEADERS,
            body: JSON.stringify(params)
        };

        let response = await fetch(server_url, data);
        response = await response.json();
        console.log("CoreMessaging", "Send", response);
        return response;

    }

    async Scopes(reset) {

        if (reset === true) {
            CoreMessaging.SCOPES = undefined;
        }

        if (CoreMessaging.SCOPES === undefined) {

            const server_url = await this.CheckServerUrl() + "decision/scopes";

            const data = {
                method: 'GET',
                headers: CoreMessaging.HEADERS
            };

            const response = await fetch(server_url, data);
            CoreMessaging.SCOPES = await response.json();
            console.log("CoreMessaging", "Scopes", CoreMessaging.SCOPES);
        }

        return CoreMessaging.SCOPES;
    }
}