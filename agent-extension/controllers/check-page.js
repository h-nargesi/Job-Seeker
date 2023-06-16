console.log("check-page");

ActionHandler.OnPageLoad = function() {
    console.log('Page', 'loaded');
    setTimeout(async function () {
        const scopes = await BackgroundMessaging.Scopes();
        const host = window.location.hostname;
        console.log('Page', "hostname:", host);
        for (let s in scopes) {
            if (host.match(new RegExp(scopes[s].domain, 'i'))) {
                console.log('Page', "matched", scopes[s].domain);
                SendingPageInfo(scopes[s]);
                break;
            }
        }
    }, 1000);

    if (document.getElementById('job-seeker-trend-list') != null) {

        document.getElementById('reset-trends').addEventListener("click", BackgroundMessaging.Scopes(true), false);

        const millisecnod = 1000;
        var ordering_interval = null;

        document.getElementById('stop-start-ordering').addEventListener("click", function () {
            if (ordering_interval == null) {
                CheckNewOrders();
                ordering_interval = setInterval(CheckNewOrders, millisecnod * 20);
                document.getElementById('stop-start-ordering').className = "btn btn-danger";
                document.getElementById('stop-start-ordering').innerText = "To Stop Ordering";

            } else {
                clearInterval(ordering_interval);
                ordering_interval = null;
                document.getElementById('stop-start-ordering').className = "btn btn-primary";
                document.getElementById('stop-start-ordering').innerText = "To Start Ordering";
            }
        }, false);

    } else if (window.location.hostname != 'localhost') ActionHandler.SetCloseTimer();
}

async function SendingPageInfo(scope) {
    console.log('Page', "sending", window.location.hostname, scope);

    const commands = await BackgroundMessaging.Send({
        agency: scope.name,
        url: window.location.href,
        content: document.documentElement.outerHTML,
    });

    console.log('Page', "commands", commands);
    ActionHandler.Handle(commands, false);
}

async function CheckNewOrders() {
    const result = await BackgroundMessaging.Orders();
    ActionHandler.Handle(result.commands, true);
}

if (window.addEventListener) window.addEventListener("load", ActionHandler.OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", ActionHandler.OnPageLoad);
else window.onload = ActionHandler.OnPageLoad;