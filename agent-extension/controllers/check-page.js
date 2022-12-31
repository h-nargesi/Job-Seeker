console.log("check-page");

ActionHandler.OnPageLoad = function() {
    console.log('Page', 'loaded');
    setTimeout(async function () {
        const scopes = await BackgroundMessaging.Scopes();
        const host = window.location.hostname;
        console.log('Page', "hostname:", host);
        for (let s in scopes) {
            console.log('Page', 'Checking for', scopes[s].domain);
            if (host.match(new RegExp(scopes[s].domain, 'i'))) {
                console.log('Page', "matched", scopes[s].domain);
                SendingPageInfo(scopes[s]);
                break;
            }
        }
    }, 5000);

    if (job_seeker_trends != null) {
        document.getElementById('reset-trends').addEventListener("click", function () {
            BackgroundMessaging.Reset();
            job_seeker_trends.innerHTML = "";
        }, false);

        CheckNewOrders();
        LoadTrands();

        const millisecnod = 1000;
        setInterval(LoadTrands, millisecnod * 3);
        var ordering_interval = setInterval(CheckNewOrders, millisecnod * 20);

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

    }
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

async function LoadTrands() {
    let html = "";

    const trends = await BackgroundMessaging.Trends();
    for (let t in trends) {
        let trend = trends[t];

        html += `
<tr>
        <td scope="row">${Number(t) + 1}</td>
        <td><a href="${trend.link}" target="_blank">${trend.agency}</a></td>
        <td><span>${trend.type}</span></td>
        <td><span>${trend.state}</span></td>
        <td><span>${trend.lastActivity}</span></td>
</tr>`;
    }

    job_seeker_trends.innerHTML = html;
}

async function CheckNewOrders() {
    const result = await BackgroundMessaging.Orders();
    ActionHandler.Handle(result.commands, true);
}

function append(element, html) {
    const temp = document.createElement('div');
    temp.innerHTML = html;

    while (temp.firstChild) {
        element.appendChild(temp.firstChild);
    }
}

var job_seeker_trends = document.getElementById('job-seeker-trend-list');

if (window.addEventListener) window.addEventListener("load", ActionHandler.OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", ActionHandler.OnPageLoad);
else window.onload = ActionHandler.OnPageLoad;