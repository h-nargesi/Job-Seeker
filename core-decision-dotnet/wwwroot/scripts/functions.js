
var job_seeker_jobs = document.getElementById('job-list');
var job_seeker_trends = document.getElementById('job-seeker-trend-list');

async function LoadTrands() {
    var response = await fetch("/report/trends", { method: 'GET' });
    job_seeker_trends.innerHTML = await response.text();
}

async function LoadJobs() {
    var response = await fetch("/report/jobs", { method: 'GET' });
    job_seeker_jobs.innerHTML = await response.text();
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

async function reset() {
    try {
        await fetch("/decision/reset", { method: 'POST' });
        job_seeker_trends.innerHTML = "";
    } catch (e) {
        console.error(e);
    }
}

async function revaluate() {
    try {
        await fetch("/job/revaluate", { method: 'POST' });
    } catch (e) {
        console.error(e);
    }
}

async function change_running(agency, running) {
    try {
        const data = {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                agency: agency,
                running: running
            })
        }
        await fetch("/decision/running", data);
        document.querySelectorAll(`button[id^="RM-${agency}"]`).forEach((button) => {
            button.className = 'btn btn-outline-primary mt-1';
        });
        document.getElementById(`RM-${agency}-${running}`).className = 'btn btn-primary mt-1';

    } catch (e) {
        console.error(e);
    }
}

function OnPageLoad() {
    const millisecnod = 1000;
    setInterval(LoadTrands, millisecnod * 3);
    setInterval(LoadJobs, millisecnod * 30);
}

if (window.addEventListener) window.addEventListener("load", OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", OnPageLoad);
else window.onload = OnPageLoad;