console.log("ASSISTANT", "core-messaging");

class CoreMessaging {

    static REQUEST_TIMEOUT = 30000;
    static HEADERS = {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        'X-Client': 'assistant'
    };

    async CheckServerUrl() {
        if (CoreMessaging.SERVER_URL === undefined) {
            CoreMessaging.SERVER_URL = await StorageHandler.ServerUrlAsync();
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
                console.error("ASSISTANT", "CoreMessaging", "error", response.status, error);
                return { error: error, status: response.status };
            }

            try {
                return JSON.parse(body);
            } catch (e) {
                console.error("ASSISTANT", "CoreMessaging", "invalid json", e);
                return { error: "invalid-json", status: response.status };
            }
        } catch (e) {
            const error = e && e.name === 'AbortError' ? "timeout" : "network";
            console.error("ASSISTANT", "CoreMessaging", error, e);
            return { error: error, status: 0 };
        } finally {
            clearTimeout(timer);
        }
    }

    async Get(path) {
        return this.FetchJson(await this.CheckServerUrl() + path, {
            method: 'GET',
            headers: await this.BuildHeaders()
        });
    }

    async Post(path, body) {
        return this.FetchJson(await this.CheckServerUrl() + path, {
            method: 'POST',
            headers: await this.BuildHeaders(),
            body: body === undefined ? undefined : JSON.stringify(body)
        });
    }

    async Jobs() {
        return this.Get("assistant/jobs");
    }

    async Applied(jobid) {
        return this.Post("assistant/applied?jobid=" + encodeURIComponent(jobid));
    }

    async MemoryList(scope, confirmed) {
        let path = "assistant/memory";
        const query = [];
        if (scope) query.push("scope=" + encodeURIComponent(scope));
        if (confirmed !== undefined && confirmed !== null) query.push("confirmed=" + confirmed);
        if (query.length) path += "?" + query.join("&");
        return this.Get(path);
    }

    async MemorySave(row) {
        return this.Post("assistant/memorysave", row);
    }

    async MemoryEdit(id, row) {
        return this.Post("assistant/memoryedit?id=" + encodeURIComponent(id), row);
    }

    async MemoryConfirm(id, confirmed) {
        return this.Post("assistant/memoryconfirm?id=" + encodeURIComponent(id) + "&confirmed=" + confirmed);
    }

    async MemoryDelete(id) {
        return this.Post("assistant/memorydelete?id=" + encodeURIComponent(id));
    }

    async MemoryBump(id) {
        return this.Post("assistant/memorybump?id=" + encodeURIComponent(id));
    }
}
