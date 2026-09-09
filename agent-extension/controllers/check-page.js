console.log("AGENT", "check-page");

ActionHandler.OnPageLoad = function () {
    console.log("AGENT", 'Page', 'loaded');
    setTimeout(async function () {
        const scopes = await BackgroundMessaging.Scopes();

        if (!scopes || scopes.error !== undefined) {
            console.error("AGENT", 'Page', "scopes failed", scopes);
            return;
        }

        const host = window.location.hostname;
        console.log("AGENT", 'Page', "hostname:", host);
        for (let s in scopes) {
            if (host.match(new RegExp(scopes[s].domain, 'i'))) {
                console.log("AGENT", 'Page', "matched", scopes[s].domain);
                SendingPageInfo(scopes[s]);
                StartHeartbeat();
                break;
            }
        }
    }, 1000);

    if (document.getElementById('job-seeker-trend-list') != null) {

        document.getElementById('reset-trends').addEventListener("click", function () { BackgroundMessaging.Scopes(true); }, false);

        const millisecnod = 1000;
        let ordering_interval = null;
        const ordering_button = document.getElementById('stop-start-ordering');

        ordering_button.addEventListener("click", function () {

            if (ordering_interval != null) {
                clearInterval(ordering_interval);
                ordering_interval = null;
            }

            let current = ordering_button.getAttribute('ordering');

            if (current !== 'true') {
                CheckNewOrders();
                ordering_interval = setInterval(CheckNewOrders, millisecnod * 20);
                current = 'true';

            } else {
                current = 'false';
            }

            ordering_button.setAttribute('ordering', current);
        }, false);

    } else if (window.location.hostname != 'localhost') ActionHandler.SetCloseTimer();
}

async function SendingPageInfo(scope) {
    if (scope.waiting) await ActionHandler.OnWait({ miliseconds: scope.waiting });

    console.log("AGENT", 'Page', "sending", window.location.hostname, scope);

    const params = {
        agency: scope.name,
        url: window.location.href,
        content: document.documentElement.outerHTML,
    };

    for (let attempt = 1; attempt <= 3; attempt++) {
        const result = await BackgroundMessaging.Send(params);

        if (!result || result.error === undefined) {
            console.log("AGENT", 'Page', "commands", result);
            ActionHandler.SetCloseTimer(result?.close_timeout_ms);
            ActionHandler.Handle(result?.commands, false);
            return;
        }

        console.error("AGENT", 'Page', "send failed", attempt, result.error, result.status);

        if (!RetryableError(result)) break;

        if (attempt < 3) await ActionHandler.OnWait({ miliseconds: attempt * 5000 });
    }
}

function RetryableError(result) {
    if (result.error === "network" || result.error === "timeout" || result.error === "no-response") return true;
    if (result.error === "http" && result.status >= 500) return true;
    return false;
}

let heartbeat_interval = null;

function StartHeartbeat() {
    if (heartbeat_interval != null) return;
    heartbeat_interval = setInterval(Heartbeat, 30000);
}

async function Heartbeat() {
    if (document.visibilityState !== 'visible') return;

    const result = await BackgroundMessaging.Heartbeat();

    if (!result || result.error !== undefined)
        console.warn("AGENT", 'Page', "heartbeat failed", result);
}

async function CheckNewOrders() {
    console.log("AGENT", 'Page', 'Taking Orders ...');
    const result = await BackgroundMessaging.Orders();

    if (!result || result.error !== undefined) {
        console.error("AGENT", 'Page', "orders failed", result);
        return;
    }

    ActionHandler.Handle(result.commands, true);
}

if (window.addEventListener) {
    window.addEventListener("load", ActionHandler.OnPageLoad, false);
    window.addEventListener("unload", function () {
        if (heartbeat_interval != null) clearInterval(heartbeat_interval);
    }, false);
}
// else if (window.attachEvent) window.attachEvent("onload", ActionHandler.OnPageLoad);
else window.onload = ActionHandler.OnPageLoad;