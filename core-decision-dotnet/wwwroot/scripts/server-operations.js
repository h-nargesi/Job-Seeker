const job_seeker_jobs = document.getElementById('job-list');
const job_seeker_trends = document.getElementById('job-seeker-trend-list');
const job_seeker_agencies = document.getElementById('job-seeker-agency-list');
const job_agency_filter = document.getElementById('job-agency-filter');
const job_country_filter = document.getElementById('job-country-filter');

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

async function LoadTrends() {
    if (!job_seeker_trends) return;
    try {
        const response = await fetch("/report/trends", { method: 'GET' });
        job_seeker_trends.innerHTML = await response.text();
    } catch (e) {
        console.error(e);
    }
}

if (job_seeker_trends) setInterval(LoadTrends, 15000);

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
        await LoadTrends();
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

async function revaluate_job(jobid) {
    try {
        await fetch("/job/revaluate?jobid=" + jobid, { method: 'POST' });
        location.reload();
    } catch (e) {
        console.error(e);
    }
}

async function requeue(jobid) {
    try {
        await fetch("/job/requeue?jobid=" + jobid, { method: 'POST' });
        location.reload();
    } catch (e) {
        console.error(e);
    }
}

async function promote(jobid) {
    try {
        await fetch("/job/promote?jobid=" + jobid, { method: 'POST' });
        location.reload();
    } catch (e) {
        console.error(e);
    }
}

async function change_running(agency, running) {
    try {
        await fetch("/decision/running", {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                agency: agency,
                running: running
            })
        });

        await LoadAgencies();

    } catch (e) {
        console.error(e);
    }
}

async function change_status(agency, field, value) {
    try {
        const body = { agency: agency };
        body[field] = value;

        await fetch("/decision/status", {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(body)
        });

        await LoadAgencies();

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

async function accept_ai(jobid) {
    try {
        await fetch(`/job/acceptai?jobid=${jobid}`, { method: 'POST' });
        location.reload();
    } catch (e) {
        console.error(e);
    }
}

async function text_op(jobid, slot, op, value_id) {
    try {
        const url = `/job/resumetext?jobid=${jobid}` +
            `&slot=${encodeURIComponent(slot)}&op=${encodeURIComponent(op)}`;

        if (op === 'live') {
            const element = document.getElementById(value_id);
            if (!element) return;

            await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(element.value)
            });
        } else {
            await fetch(url, { method: 'POST' });
        }

        location.reload();

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

function filterChanged(event) {
    if (event.code === "Enter")
        LoadJobs();
}