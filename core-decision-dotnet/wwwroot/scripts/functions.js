
var job_seeker_trends = document.getElementById('job-seeker-trend-list');
var reset_trends = document.getElementById('reset-trends');

async function LoadTrands() {

    try {
        let html = "";

        let response = await fetch("/report/trends", { method: 'GET' });
        const trends = await response.json();

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

    } catch (e) {
        console.error(e);
        return {};
    }
}

async function apply(jobid) {
    try {
        await fetch("/job/apply?jobid=" + jobid, { method: 'POST' });
        document.getElementById('Job_' + jobid).className = 'applied';

    } catch (e) {
        console.error(e);
    }
}

async function reject(jobid) {
    try {
        await fetch("/job/reject?jobid=" + jobid, { method: 'POST' });
        document.getElementById('Job_' + jobid).className = 'rejected';

    } catch (e) {
        console.error(e);
    }
}

function OnPageLoad() {
    LoadTrands();

    const millisecnod = 1000;
    setInterval(LoadTrands, millisecnod * 3);
}

if (window.addEventListener) window.addEventListener("load", OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", OnPageLoad);
else window.onload = OnPageLoad;