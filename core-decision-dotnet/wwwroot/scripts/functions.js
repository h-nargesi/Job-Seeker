
let job_seeker_jobs = document.getElementById('job-list');
let job_seeker_trends = document.getElementById('job-seeker-trend-list');
let interval_trends = null;
let interval_jobs = null;

async function LoadTrands() {
    let response = await fetch("/report/trends", { method: 'GET' });
    job_seeker_trends.innerHTML = await response.text();
}

async function LoadJobs() {
    let response = await fetch("/report/jobs", { method: 'GET' });
    job_seeker_jobs.innerHTML = await response.text();
}

async function apply(jobid) {
    try {
        await fetch("/job/apply?jobid=" + jobid, { method: 'POST' });
        document.getElementById('Job_' + jobid).className = 'applied';
        LoadJobs();
    } catch (e) {
        console.error(e);
    }
}

async function reject(jobid) {
    try {
        await fetch("/job/reject?jobid=" + jobid, { method: 'POST' });
        document.getElementById('Job_' + jobid).className = 'rejected';
        LoadJobs();
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

function ordering() {
    if (interval_trends == null || interval_jobs == null) {
        const millisecnod = 1000;
        interval_trends = setInterval(LoadTrands, millisecnod * 3);
        interval_jobs = setInterval(LoadJobs, millisecnod * 30);
        document.getElementById('stop-start-ordering').className = "btn btn-danger";
        document.getElementById('stop-start-ordering').innerText = "To Stop Ordering";

    } else {
        clearInterval(interval_trends);
        clearInterval(interval_jobs);
        interval_trends = null;
        interval_jobs = null;
        document.getElementById('stop-start-ordering').className = "btn btn-primary";
        document.getElementById('stop-start-ordering').innerText = "To Start Ordering";
    }
}
