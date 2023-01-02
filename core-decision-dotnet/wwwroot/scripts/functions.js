
var job_seeker_trends = document.getElementById('job-seeker-trend-list');
var reset_trends = document.getElementById('reset-trends');

function OnPageLoad () {
    reset_trends.addEventListener("click", function () {
        BackgroundMessaging.Reset();
        job_seeker_trends.innerHTML = "";
    }, false);

    LoadTrands();

    const millisecnod = 1000;
    setInterval(LoadTrands, millisecnod * 3);
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

function append(element, html) {
    const temp = document.createElement('div');
    temp.innerHTML = html;

    while (temp.firstChild) {
        element.appendChild(temp.firstChild);
    }
}

if (window.addEventListener) window.addEventListener("load", OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", OnPageLoad);
else window.onload = OnPageLoad;