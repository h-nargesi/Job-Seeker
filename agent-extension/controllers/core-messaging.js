console.log("AGENT", "core-messaging");

class CoreMessaging {

    static SERVER_URL;
    static API_KEY;
    static SCOPES;
    static SCOPES_AT = 0;
    static SCOPES_TTL = 60000;
    static SCOPES_CACHE_KEY = "SCOPES_CACHE";
    static REQUEST_TIMEOUT = 30000;
    static HEADERS = {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        'X-Client': 'search'
    };

    async CheckServerUrl() {
        if (CoreMessaging.SERVER_URL === undefined) {
            CoreMessaging.SERVER_URL = await StorageHandler.ServerUrlAsync();
            if (!CoreMessaging.SERVER_URL.endsWith('/')) CoreMessaging.SERVER_URL += '/';
        }

        return CoreMessaging.SERVER_URL;
    }

    async CheckApiKey() {
        if (CoreMessaging.API_KEY === undefined) {
            CoreMessaging.API_KEY = await StorageHandler.ApiKeyAsync();
        }

        return CoreMessaging.API_KEY;
    }

    async BuildHeaders() {
        const api_key = await this.CheckApiKey();
        const headers = Object.assign({}, CoreMessaging.HEADERS);
        if (api_key) headers['X-API-Key'] = api_key;
        return headers;
    }

    async FetchJson(url, data) {
        const controller = new AbortController();
        const timer = setTimeout(function () { controller.abort(); }, CoreMessaging.REQUEST_TIMEOUT);

        data.signal = controller.signal;

        try {
            const response = await fetch(url, data);
            const body = await response.text();

            if (!response.ok) {
                let error = "http";
                try { error = JSON.parse(body).error ?? error; } catch { }
                console.error("AGENT", "CoreMessaging", "FetchJson", "error", response.status, error);
                return { error: error, status: response.status };
            }

            try {
                return JSON.parse(body);
            } catch (e) {
                console.error("AGENT", "CoreMessaging", "FetchJson", "invalid json", e);
                return { error: "invalid-json", status: response.status };
            }

        } catch (e) {
            const error = e && e.name === 'AbortError' ? "timeout" : "network";
            console.error("AGENT", "CoreMessaging", "FetchJson", error, e);
            return { error: error, status: 0 };
        } finally {
            clearTimeout(timer);
        }
    }

    async Send(params) {
        const server_url = await this.CheckServerUrl() + "decision/take";

        const result = await this.FetchJson(server_url, {
            method: 'POST',
            headers: await this.BuildHeaders(),
            body: JSON.stringify(params)
        });

        console.log("AGENT", "CoreMessaging", "Send", result);
        return result;
    }

    async Heartbeat(params) {
        const server_url = await this.CheckServerUrl() + "decision/heartbeat";

        const result = await this.FetchJson(server_url, {
            method: 'POST',
            headers: await this.BuildHeaders(),
            body: JSON.stringify(params)
        });

        return result;
    }

    async Scopes() {
        try {
            const fresh = CoreMessaging.SCOPES !== undefined && Date.now() - CoreMessaging.SCOPES_AT < CoreMessaging.SCOPES_TTL;

            if (!fresh) {
                let scopes = await CoreMessaging.ReadScopesCache();

                if (scopes === undefined) {
                    const server_url = await this.CheckServerUrl() + "decision/scopes";

                    const result = await this.FetchJson(server_url, {
                        method: 'GET',
                        headers: await this.BuildHeaders()
                    });

                    if (result.error !== undefined) return result;

                    scopes = result;
                    await CoreMessaging.WriteScopesCache(scopes);
                    console.log("AGENT", "CoreMessaging", "Scopes", scopes);
                }

                CoreMessaging.SCOPES = scopes;
                CoreMessaging.SCOPES_AT = Date.now();
            }

            return CoreMessaging.SCOPES;

        } catch (e) {
            console.error("AGENT", "CoreMessaging", "Scopes", e);
            return { error: "client", status: 0 };
        }
    }

    static async ReadScopesCache() {
        try {
            const items = await chrome.storage.session.get(CoreMessaging.SCOPES_CACHE_KEY);
            const cached = items && items[CoreMessaging.SCOPES_CACHE_KEY];

            if (cached && typeof cached === "object" && Date.now() - cached.at < CoreMessaging.SCOPES_TTL) {
                return cached.scopes;
            }
        } catch (e) { }

        return undefined;
    }

    static async WriteScopesCache(scopes) {
        try {
            await chrome.storage.session.set({ [CoreMessaging.SCOPES_CACHE_KEY]: { at: Date.now(), scopes: scopes } });
        } catch (e) { }
    }

    async Orders() {
        try {
            const server_url = await this.CheckServerUrl() + "decision/orders";

            const result = await this.FetchJson(server_url, {
                method: 'GET',
                headers: await this.BuildHeaders()
            });

            console.log("AGENT", "CoreMessaging", "Orders", result);
            return result;

        } catch (e) {
            console.error("AGENT", "CoreMessaging", "Orders", e);
            return { error: "client", status: 0 };
        }
    }
}