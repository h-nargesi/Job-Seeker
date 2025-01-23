const job_seeker_jobs = document.getElementById('job-list');
const job_seeker_trends = document.getElementById('job-seeker-trend-list');
const job_seeker_agencies = document.getElementById('job-seeker-agency-list');
const job_agency_filter = document.getElementById('job-agency-filter');
const job_country_filter = document.getElementById('job-country-filter');
let intervals = null;

async function LoadTrands() {
    const response = await fetch("/report/trends", { method: 'GET' });
    job_seeker_trends.innerHTML = await response.text();
}

async function LoadJobs() {
    let query = "";

    const agency_filter = job_agency_filter?.value;
    if (agency_filter) query += `&agencies=${agency_filter}`;

    const country_filter = job_country_filter?.value;
    if (country_filter) query += `&countries=${country_filter}`;

    if (query.length > 0) query = "?" + query.substring(1);

    const response = await fetch(`/report/jobs${query}`, { method: 'GET' });
    job_seeker_jobs.innerHTML = await response.text();
}

async function LoadAgencies() {
    const response = await fetch("/report/agencies", { method: 'GET' });
    job_seeker_agencies.innerHTML = await response.text();
}

async function apply(jobid) {
    try {
        await fetch("/job/apply?jobid=" + jobid, { method: 'POST' });
        document.getElementById('Job_' + jobid).className = 'applied';
        LoadJobs();
        LoadAgencies();
    } catch (e) {
        console.error(e);
    }
}

async function reject(jobid) {
    try {
        await fetch("/job/reject?jobid=" + jobid, { method: 'POST' });
        document.getElementById('Job_' + jobid).className = 'rejected';
        LoadJobs();
        LoadAgencies();
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
        const current_element = document.getElementById(`RM-${agency}-${running}`);
        if (!current_element) return;

        const enabled = current_element.classList.contains('btn-success');

        const data = {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                agency: agency,
                running: enabled ? null : running
            })
        }
        await fetch("/decision/running", data);

        document.querySelectorAll(`button[id^="RM-${agency}"]`).forEach((button) => {
            button.className = 'btn btn-outline-info mt-1';
        });
        if (!enabled) current_element.className = 'btn btn-success mt-1';

    } catch (e) {
        console.error(e);
    }
}

async function submit_options(job_id, json_id) {
    try {
        const resume_element = document.getElementById(json_id);
        if (!resume_element) return;

        const data = {
            method: 'POST',
            headers: {
                'Accept': 'plain/text',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(resume_element.value)
        }
        const response = await fetch(`/job/options?jobid=${job_id}`, data);
        resume_element.value = await response.text();

    } catch (e) {
        console.error(e);
    }
}

async function clean() {
    try {
        await fetch("/job/clean", { method: 'POST' });
        LoadJobs();
    } catch (e) {
        console.error(e);
    }
}

function ordering() {
    if (intervals == null) {
        const millisecnod = 1000;
        intervals = [];
        intervals.push(setInterval(LoadTrands, millisecnod * 3));
        intervals.push(setInterval(LoadJobs, millisecnod * 30));
        intervals.push(setInterval(LoadAgencies, millisecnod * 10));
        document.getElementById('stop-start-ordering').className = "btn btn-danger m-1";
        document.getElementById('stop-start-ordering').innerText = "To Stop Ordering";

    } else {
        for (let i in intervals)
            clearInterval(intervals[i]);
        intervals = null;
        document.getElementById('stop-start-ordering').className = "btn btn-primary m-1";
        document.getElementById('stop-start-ordering').innerText = "To Start Ordering";
    }
}

function filterChanged(event) {
    if (event.code === "Enter")
        LoadJobs();
}