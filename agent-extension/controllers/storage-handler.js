console.log("AGENT", "storage-handler");

class StorageHandler {

    static SERVER_URL = "SERVER_URL";
    static SERVER_URL_DEFAULT = "http://localhost:8081/";
    static API_KEY = "API_KEY";
    static API_KEY_DEFAULT = "";
    static ORDERING = "ORDERING";
    static ORDERING_DEFAULT = false;
    static LAST_ORDERS = "LAST_ORDERS";

    static async Get(key, default_value) {
        return new Promise(function (resolve, reject) {
            chrome.storage.local.get(key, function (items) {
                if (chrome.runtime.lastError) {
                    console.log("AGENT", chrome.runtime.lastError.message);
                    reject(chrome.runtime.lastError.message);
                } else {
                    resolve(items[key] ?? default_value);
                }
            });
        });
    }

    static Set(key, value) {
        let setting = {};
        setting[key] = value;
        chrome.storage.local.set(setting);
    }

    static async ServerUrlAsync() {
        return (async () => String(await StorageHandler.Get(StorageHandler.SERVER_URL, StorageHandler.SERVER_URL_DEFAULT)))();
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

    static async OrderingAsync() {
        return (async () => Boolean(await StorageHandler.Get(StorageHandler.ORDERING, StorageHandler.ORDERING_DEFAULT)))();
    }

    static set Ordering(value) {
        StorageHandler.Set(StorageHandler.ORDERING, Boolean(value));
    }

    static async LastOrdersAsync() {
        return (async () => await StorageHandler.Get(StorageHandler.LAST_ORDERS, null))();
    }
}