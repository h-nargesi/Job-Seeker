const dashboard_chart_body = document.getElementById('dashboard-chart-body');

const DASHBOARD_CHART_REFRESH_MS = 15000;

const DAILY_SEGMENTS = [
    { key: 'saved', state: 'saved', label: 'Saved' },
    { key: 'revaluation', state: 'revaluation', label: 'Revaluation' },
    { key: 'gateRejected', state: 'notApprovedRegex', label: 'Gate-rejected' },
    { key: 'inAi', state: 'aiPending', label: 'In-AI' },
    { key: 'attention', state: 'attention', label: 'Attention' },
    { key: 'applied', state: 'applied', label: 'Applied' },
    { key: 'rejected', state: 'rejected', label: 'Rejected' },
    { key: 'failed', state: 'failed', label: 'Failed' }
];

const VELOCITY_SERIES = [
    { key: 'applied', state: 'applied', label: 'Applied' },
    { key: 'rejected', state: 'rejected', label: 'Rejected' }
];

let loading_stats_daily = false;
let daily_stacked_chart = null;
let velocity_chart = null;
let last_stats_daily = null;

function chart_text_color() {
    return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? '#adb5bd' : '#212529';
}

function chart_grid_color() {
    return document.documentElement.getAttribute('data-bs-theme') === 'dark'
        ? 'rgba(255, 255, 255, 0.12)'
        : 'rgba(0, 0, 0, 0.08)';
}

function base_options() {
    return {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { position: 'bottom', labels: { color: chart_text_color(), boxWidth: 10, boxHeight: 10 } }
        },
        scales: {
            x: { grid: { color: chart_grid_color() }, ticks: { color: chart_text_color(), maxRotation: 0 } },
            y: { beginAtZero: true, grid: { color: chart_grid_color() }, ticks: { color: chart_text_color(), precision: 0 } }
        }
    };
}

function make_daily_chart() {
    const canvas = document.getElementById('daily-stacked-chart');
    if (!canvas || !window.Chart) return null;

    const options = base_options();
    options.scales.x.stacked = true;
    options.scales.y.stacked = true;

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: [],
            datasets: DAILY_SEGMENTS.map(segment => ({
                label: segment.label,
                data: [],
                backgroundColor: job_state_color(segment.state),
                stack: 'jobs',
                borderWidth: 0
            }))
        },
        options: options
    });
}

function make_velocity_chart() {
    const canvas = document.getElementById('velocity-chart');
    if (!canvas || !window.Chart) return null;

    return new Chart(canvas, {
        type: 'line',
        data: {
            labels: [],
            datasets: VELOCITY_SERIES.map(series => ({
                label: series.label,
                data: [],
                borderColor: job_state_color(series.state),
                backgroundColor: job_state_color(series.state),
                tension: 0.25,
                pointRadius: 2,
                borderWidth: 2
            }))
        },
        options: base_options()
    });
}

function render_daily_stacked(items) {
    if (!items || items.length === 0) return;

    daily_stacked_chart = daily_stacked_chart || make_daily_chart();
    if (!daily_stacked_chart) return;

    daily_stacked_chart.data.labels = items.map(item => item.day.slice(5));
    DAILY_SEGMENTS.forEach((segment, index) => {
        daily_stacked_chart.data.datasets[index].data = items.map(item => item[segment.key] ?? 0);
    });
    daily_stacked_chart.update();
}

function render_velocity(items) {
    if (!items || items.length === 0) return;

    velocity_chart = velocity_chart || make_velocity_chart();
    if (!velocity_chart) return;

    velocity_chart.data.labels = items.map(item => item.day.slice(5));
    VELOCITY_SERIES.forEach((series, index) => {
        velocity_chart.data.datasets[index].data = items.map(item => item[series.key] ?? 0);
    });
    velocity_chart.update();
}

function set_kpi(id, value) {
    const element = document.getElementById(id);
    if (element) element.textContent = value;
}

function render_kpis(kpis) {
    if (!kpis) return;

    const days = value => value == null ? '—' : `${Number(value).toFixed(1)} d`;

    set_kpi('kpi-avg-disposition', days(kpis.avgDispositionDays));
    set_kpi('kpi-attention-backlog', String(kpis.attentionBacklog ?? 0));
    set_kpi('kpi-attention-age', days(kpis.attentionAvgAgeDays));
}

function render_stats_daily(data) {
    last_stats_daily = data;

    render_kpis(data.kpis);
    render_daily_stacked(data.dailyStacked);
    render_velocity(data.velocity);
}

async function LoadStatsDaily() {
    if (loading_stats_daily || !dashboard_chart_body) return;
    loading_stats_daily = true;

    try {
        const response = await fetch('/report/statsdaily', { method: 'GET' });
        const data = await response.json();

        if (window.Chart && data) render_stats_daily(data);

    } catch (e) {
        console.error(e);
    } finally {
        loading_stats_daily = false;
    }
}

function rebuild_charts_on_theme_change() {
    if (daily_stacked_chart) {
        daily_stacked_chart.destroy();
        daily_stacked_chart = null;
    }

    if (velocity_chart) {
        velocity_chart.destroy();
        velocity_chart = null;
    }

    if (last_stats_daily) render_stats_daily(last_stats_daily);
}

if (dashboard_chart_body) {
    new MutationObserver(rebuild_charts_on_theme_change)
        .observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });

    LoadStatsDaily();
    setInterval(LoadStatsDaily, DASHBOARD_CHART_REFRESH_MS);
}
