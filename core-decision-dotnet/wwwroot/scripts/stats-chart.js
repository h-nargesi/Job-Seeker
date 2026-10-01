const stats_charts_body = document.getElementById('stats-charts-body');

const FUNNEL_STAGES = [
    { key: 'saved', state: 'saved', label: 'Saved' },
    { key: 'analyzed', state: 'aiPending', label: 'Analyzed' },
    { key: 'attention', state: 'attention', label: 'Attention' },
    { key: 'applied', state: 'applied', label: 'Applied' }
];

const HEALTH_SERIES = [
    { key: 'aiPending', state: 'aiPending', label: 'AiPending' },
    { key: 'aiError', state: 'aiError', label: 'AIError' }
];

const YIELD_SERIES = [
    { key: 'analyzingRate', label: 'Analyzed %', color: '#0dcaf0' },
    { key: 'acceptingRate', label: 'Accepted %', color: '#ffc107' }
];

const SKILL_COLORS = { have: '#198754', missing: '#dc3545' };

let funnel_chart = null;
let yield_chart = null;
let health_chart = null;
let skills_chart = null;
let last_stats_full = null;
let funnel_selection = '';

function chart_text_color() {
    return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? '#adb5bd' : '#212529';
}

function chart_grid_color() {
    return document.documentElement.getAttribute('data-bs-theme') === 'dark'
        ? 'rgba(255, 255, 255, 0.12)'
        : 'rgba(0, 0, 0, 0.08)';
}

function base_options(horizontal) {
    const options = {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { position: 'bottom', labels: { color: chart_text_color(), boxWidth: 10, boxHeight: 10 } }
        },
        scales: {
            x: { beginAtZero: true, grid: { color: chart_grid_color() }, ticks: { color: chart_text_color(), precision: 0 } },
            y: { beginAtZero: true, grid: { color: chart_grid_color() }, ticks: { color: chart_text_color(), precision: 0 } }
        }
    };

    if (horizontal) options.scales.x.ticks.maxRotation = 0;

    return options;
}

function make_funnel_chart() {
    const canvas = document.getElementById('funnel-chart');
    if (!canvas || !window.Chart) return null;

    const options = base_options(true);
    options.indexAxis = 'y';

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: FUNNEL_STAGES.map(stage => stage.label),
            datasets: [{
                label: 'Jobs',
                data: [],
                backgroundColor: FUNNEL_STAGES.map(stage => job_state_color(stage.state)),
                borderWidth: 0
            }]
        },
        options: options
    });
}

function make_yield_chart() {
    const canvas = document.getElementById('agency-yield-chart');
    if (!canvas || !window.Chart) return null;

    const options = base_options(true);
    options.indexAxis = 'y';
    options.scales.x.max = 100;
    options.plugins.tooltip = {
        callbacks: {
            afterBody: items => {
                const row = last_stats_full?.agencyYield?.[items[0]?.dataIndex];
                return row ? [
                    `Jobs: ${row.jobCount}`,
                    `Analyzed: ${row.analyzed}`,
                    `Accepted: ${row.accepted}`,
                    `Applied: ${row.applied}`
                ] : [];
            }
        }
    };

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: [],
            datasets: YIELD_SERIES.map(series => ({
                label: series.label,
                data: [],
                backgroundColor: series.color,
                borderWidth: 0
            }))
        },
        options: options
    });
}

function make_health_chart() {
    const canvas = document.getElementById('pipeline-health-chart');
    if (!canvas || !window.Chart) return null;

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: [],
            datasets: HEALTH_SERIES.map(series => ({
                label: series.label,
                data: [],
                backgroundColor: job_state_color(series.state),
                borderWidth: 0
            }))
        },
        options: base_options()
    });
}

function make_skills_chart() {
    const canvas = document.getElementById('skills-gap-chart');
    if (!canvas || !window.Chart) return null;

    const options = base_options(true);
    options.indexAxis = 'y';
    options.plugins.legend.display = false;
    options.plugins.tooltip = {
        callbacks: {
            label: item => {
                const row = last_stats_full?.skillsGap?.top?.[item.dataIndex];
                if (!row) return '';
                return `${item.parsed.x} jobs — ${row.have ? 'have' : 'missing'}`;
            }
        }
    };

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: [],
            datasets: [{ label: 'Jobs', data: [], backgroundColor: [], borderWidth: 0 }]
        },
        options: options
    });
}

function funnel_stages_for_selection(data) {
    if (!data) return null;

    if (!funnel_selection) return data.overall;

    return data.agencies.find(agency => agency.title === funnel_selection)?.stages ?? null;
}

function render_funnel(data) {
    if (!data) return;

    const select = document.getElementById('funnel-agency-select');
    if (select && select.options.length === 0) {
        select.add(new Option('All', ''));
        (data.agencies ?? []).forEach(agency => select.add(new Option(agency.title, agency.title)));
        select.onchange = () => {
            funnel_selection = select.value;
            render_funnel(last_stats_full?.funnel);
        };
    }

    const stages = funnel_stages_for_selection(data);
    if (!stages) return;

    funnel_chart = funnel_chart || make_funnel_chart();
    if (!funnel_chart) return;

    funnel_chart.data.datasets[0].data = FUNNEL_STAGES.map(stage => stages[stage.key] ?? 0);
    funnel_chart.update();
}

function render_yield(items) {
    if (!items || items.length === 0) return;

    yield_chart = yield_chart || make_yield_chart();
    if (!yield_chart) return;

    yield_chart.data.labels = items.map(item => item.title);
    YIELD_SERIES.forEach((series, index) => {
        yield_chart.data.datasets[index].data = items.map(item => item[series.key] ?? 0);
    });
    yield_chart.update();
}

function render_health(items) {
    if (!items || items.length === 0) return;

    health_chart = health_chart || make_health_chart();
    if (!health_chart) return;

    health_chart.data.labels = items.map(item => item.day.slice(5));
    HEALTH_SERIES.forEach((series, index) => {
        health_chart.data.datasets[index].data = items.map(item => item[series.key] ?? 0);
    });
    health_chart.update();
}

function render_skills(gap) {
    if (!gap || !gap.top || gap.top.length === 0) return;

    skills_chart = skills_chart || make_skills_chart();
    if (!skills_chart) return;

    skills_chart.data.labels = gap.top.map(item => item.skill);
    skills_chart.data.datasets[0].data = gap.top.map(item => item.jobs);
    skills_chart.data.datasets[0].backgroundColor = gap.top.map(item =>
        item.have ? SKILL_COLORS.have : SKILL_COLORS.missing);
    skills_chart.update();
}

function render_stats_full(data) {
    last_stats_full = data;

    render_funnel(data.funnel);
    render_yield(data.agencyYield);
    render_health(data.pipelineHealth);
    render_skills(data.skillsGap);

    if (typeof render_analysis === 'function') render_analysis(data);
}

function stats_full_url() {
    const params = new URLSearchParams();

    const agencies = document.getElementById('stats-agency-filter')?.value.trim();
    const countries = document.getElementById('stats-country-filter')?.value.trim();

    if (agencies) params.set('agencies', agencies);
    if (countries) params.set('countries', countries);

    const query = params.toString();
    return '/report/statsfull' + (query ? '?' + query : '');
}

async function LoadStatsFull() {
    if (!stats_charts_body || !window.Chart) return;

    try {
        const response = await fetch(stats_full_url(), { method: 'GET' });
        const data = await response.json();

        if (data) render_stats_full(data);

    } catch (e) {
        console.error(e);
    }
}

function rebuild_charts_on_theme_change() {
    if (funnel_chart) {
        funnel_chart.destroy();
        funnel_chart = null;
    }

    if (yield_chart) {
        yield_chart.destroy();
        yield_chart = null;
    }

    if (health_chart) {
        health_chart.destroy();
        health_chart = null;
    }

    if (skills_chart) {
        skills_chart.destroy();
        skills_chart = null;
    }

    if (typeof rebuild_analysis_charts === 'function') rebuild_analysis_charts();

    if (last_stats_full) render_stats_full(last_stats_full);
}

if (stats_charts_body) {
    new MutationObserver(rebuild_charts_on_theme_change)
        .observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });

    LoadStatsFull();
}
