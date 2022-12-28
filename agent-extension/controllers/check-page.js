console.log("check-page");

function OnPageLoad() {
    console.log('Page', 'loaded');
    setTimeout(async function () {
        var scopes = await BackgroundMessaging.Scopes();
        var host = window.location.hostname;
        console.log('Page', "hostname:", host);
        for (let s in scopes) {
            if (host.match(new RegExp(scopes[s].domain, 'i'))) {
                console.log('Page', "matched", scopes[s].domain);
                SendingPageInfo(scopes[s]);
                break;
            }
        }
    }, 5000);

    if (job_seeker_trends != null) {
        document.getElementById('reload-trends').addEventListener("click", function() {
            BackgroundMessaging.Reload();
            job_seeker_trends.innerHTML = "";
        }, false);

        LoadTrands();
        CheckNewOrders();

        var millisecnod = 1000;
        setInterval(LoadTrands, millisecnod * 5);
        setInterval(CheckNewOrders, millisecnod * 30);
    }
}

async function SendingPageInfo(scope) {
    console.log('Page', "sending", window.location.hostname, scope);

    const commands = await BackgroundMessaging.Send({
        agency: scope.name,
        url: window.location.href,
        content: document.body.innerHTML,
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
    var temp = document.createElement('div');
    temp.innerHTML = html;

    while (temp.firstChild) {
        element.appendChild(temp.firstChild);
    }
}

var job_seeker_trends = document.getElementById('job-seeker-trend-list');

if (window.addEventListener) window.addEventListener("load", OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", OnPageLoad);
else window.onload = OnPageLoad;