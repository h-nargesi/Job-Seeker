console.log("menu.js");

const Manifest = chrome.runtime.getManifest();
const OpenServer = document.getElementById('OpenServer');
const ServerUrl = document.getElementById('ServerUrl');
const ManifestTitle = document.getElementById('ManifestTitle');
const ManifestDescr = document.getElementById('ManifestDescr');

ServerUrl.addEventListener("keyup", function (event) {
    console.log("ServerUrl", "keyup", event.keyCode);
    event.preventDefault();
    if (event.keyCode === 13) {
        const url = ServerUrl.value;
        if (url) {
            url = url.trim();
            if (url.endsWith('/')) url = url.substr(0, url.length - 1);
        }
        console.log("Menu", "ServerUrl", ServerUrl.value);
        StorageHandler.ServerUrl = ServerUrl.value;
    }
});

OpenServer.addEventListener("click", function () {
    window.open(ServerUrl.value);
});

async function LoadData() {
    console.log("Menu", "LoadData");
    ServerUrl.value = await StorageHandler.ServerUrlAsync();
    ManifestTitle.innerText = Manifest.name;
    ManifestDescr.innerText = Manifest.description ?? "";
}

LoadData();
