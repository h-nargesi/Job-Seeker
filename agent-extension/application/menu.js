console.log("AGENT", "menu.js");

const Manifest = chrome.runtime.getManifest();
const OpenServer = document.getElementById('OpenServer');
const ServerUrl = document.getElementById('ServerUrl');
const ApiKey = document.getElementById('ApiKey');
const ManifestTitle = document.getElementById('ManifestTitle');
const ManifestDescr = document.getElementById('ManifestDescr');

ServerUrl.addEventListener("keyup", function (event) {
    event.preventDefault();
    if (event.keyCode === 13) {
        let url = ServerUrl.value;
        if (url) {
            url = url.toString().trim();
            while (url.endsWith('/')) url = url.substr(0, url.length - 1);
            ServerUrl.value = url;
        }
        console.log("AGENT", "Menu", "ServerUrl", url);
        StorageHandler.ServerUrl = url;
    }
});

ApiKey.addEventListener("keyup", function (event) {
    event.preventDefault();
    if (event.keyCode === 13) {
        const key = ApiKey.value ? ApiKey.value.toString().trim() : "";
        console.log("AGENT", "Menu", "ApiKey");
        StorageHandler.ApiKey = key;
    }
});

OpenServer.addEventListener("click", async function () {
    window.open(await StorageHandler.ServerUrlAsync());
});

async function LoadData() {
    console.log("AGENT", "Menu", "LoadData");
    ServerUrl.value = await StorageHandler.ServerUrlAsync();
    ApiKey.value = await StorageHandler.ApiKeyAsync();
    ManifestTitle.innerText = Manifest.name;
    ManifestDescr.innerText = Manifest.description ?? "";
}

LoadData();
