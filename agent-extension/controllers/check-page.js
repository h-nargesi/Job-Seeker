console.log("check-page");

function OnPageLoad() {
    console.log('Page is loaded');
    setTimeout(async function () {
        var scopes = await BackgroundMessaging.Scopes();
        var host = window.location.hostname;
        console.log("hostname:", host);
        for (let s in scopes) {
            if (host.match(new RegExp(scopes[s].domain, 'i'))) {
                console.log("matched", scopes[s].domain);
                SendingPageInfo(scopes[s]);
                break;
            }
        }
    }, 5000);
}

async function SendingPageInfo(scope) {
    console.log("SendingPageInfo", window.location.hostname, scope);

    const commands = await BackgroundMessaging.Send({
        agency: scope.name,
        url: window.location.href,
        content: document.body.innerHTML,
    });

    console.log("SendingPageInfo", "Command", commands);
    ActionHandler.Handle(commands);
}

if (window.addEventListener) window.addEventListener("load", OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", OnPageLoad);
else window.onload = OnPageLoad;
