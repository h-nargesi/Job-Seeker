console.log("check-page");

ActionHandler.OnPageLoad = function () {
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
                current = 'false';

            } else {
                current = 'true';
            }

            ordering_button.setAttribute('ordering', current);
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