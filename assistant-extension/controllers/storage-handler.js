console.log("ASSISTANT", "storage-handler");

class StorageHandler {

    static SERVER_URL = "SERVER_URL";
    static SERVER_URL_DEFAULT = "http://localhost:8081/";
    static API_KEY = "API_KEY";
    static API_KEY_DEFAULT = "";
    static LLAMA_URL = "LLAMA_URL";
    static LLAMA_URL_DEFAULT = "http://localhost:8082/";
    static LLAMA_MODEL = "LLAMA_MODEL";
    static LLAMA_MODEL_DEFAULT = "Qwen3-30B-A3B-Q5_K_M";
    static MODE_OVERRIDE = "MODE_OVERRIDE";
    static PENDING_DIFFS = "PENDING_DIFFS";
    static CHAT_LOG = "CHAT_LOG";
    static COMPOSE_DRAFTS = "COMPOSE_DRAFTS";

    static async Get(key, default_value) {
        return new Promise(function (resolve, reject) {
            chrome.storage.local.get(key, function (items) {
                if (chrome.runtime.lastError) {
                    console.log("ASSISTANT", chrome.runtime.lastError.message);
                    reject(chrome.runtime.lastError.message);
                } else {
                    resolve(items[key] ?? default_value);
                }
            });
        });
    }

    static Set(key, value) {
        const setting = {};
        setting[key] = value;
        chrome.storage.local.set(setting);
    }

    static async GetSession(key, default_value) {
        return new Promise(function (resolve, reject) {
            chrome.storage.session.get(key, function (items) {
                if (chrome.runtime.lastError) {
                    console.log("ASSISTANT", chrome.runtime.lastError.message);
                    reject(chrome.runtime.lastError.message);
                } else {
                    resolve(items[key] ?? default_value);
                }
            });
        });
    }

    static async SetSession(key, value) {
        return new Promise(function (resolve) {
            chrome.storage.session.set({ [key]: value }, function () {
                resolve();
            });
        });
    }

    static async ServerUrlAsync() {
        const url = String(await StorageHandler.Get(StorageHandler.SERVER_URL, StorageHandler.SERVER_URL_DEFAULT));
        return url.endsWith('/') ? url : url + '/';
    }

    static set ServerUrl(value) {
        StorageHandler.Set(StorageHandler.SERVER_URL, value);
    }

    static async ApiKeyAsync() {
        return (async () => String(await StorageHandler.Get(StorageHandler.API_KEY, StorageHandler.API_KEY_DEFAULT)))();
    }

    static set ApiKey(value) {
        StorageHandler.Set(StorageHandler.API_KEY, value);
    }

    static async LlamaUrlAsync() {
        const url = String(await StorageHandler.Get(StorageHandler.LLAMA_URL, StorageHandler.LLAMA_URL_DEFAULT));
        return url.endsWith('/') ? url : url + '/';
    }

    static set LlamaUrl(value) {
        StorageHandler.Set(StorageHandler.LLAMA_URL, value);
    }

    static async LlamaModelAsync() {
        return (async () => String(await StorageHandler.Get(StorageHandler.LLAMA_MODEL, StorageHandler.LLAMA_MODEL_DEFAULT)))();
    }

    static set LlamaModel(value) {
        StorageHandler.Set(StorageHandler.LLAMA_MODEL, value);
    }
}
