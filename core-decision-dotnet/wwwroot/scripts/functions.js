
var job_seeker_jobs = document.getElementById('job-list');
var job_seeker_trends = document.getElementById('job-seeker-trend-list');
var reset_trends = document.getElementById('reset-trends');

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

function OnPageLoad() {
    const millisecnod = 1000;
    setInterval(LoadTrands, millisecnod * 3);
    setInterval(LoadJobs, millisecnod * 7);
}

if (window.addEventListener) window.addEventListener("load", OnPageLoad, false);
// else if (window.attachEvent) window.attachEvent("onload", OnPageLoad);
else window.onload = OnPageLoad;