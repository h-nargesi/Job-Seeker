const monitor_page_body = document.getElementById('ai-monitor-body');

let monitor_stages_chart = null;
let monitor_trends_chart = null;

function monitor_chart_text_color() {
    return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? '#adb5bd' : '#212529';
}

function monitor_chart_grid_color() {
    return document.documentElement.getAttribute('data-bs-theme') === 'dark'
        ? 'rgba(255, 255, 255, 0.12)'
        : 'rgba(0, 0, 0, 0.08)';
}

function monitor_base_options() {
    return {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { position: 'bottom', labels: { color: monitor_chart_text_color(), boxWidth: 10, boxHeight: 10 } }
        },
        scales: {
            x: { stacked: false, grid: { color: monitor_chart_grid_color() }, ticks: { color: monitor_chart_text_color(), maxRotation: 0 } },
            y: { beginAtZero: true, grid: { color: monitor_chart_grid_color() }, ticks: { color: monitor_chart_text_color(), precision: 0 } }
        }
    };
}

function monitor_read_data(id) {
    const element = document.getElementById(id);
    if (!element) return null;

    try {
        return JSON.parse(element.textContent);
    } catch (e) {
        console.error(e);
        return null;
    }
}

function monitor_make_stages_chart(data) {
    const canvas = document.getElementById('monitor-stages-chart');
    if (!canvas || !window.Chart || !data || data.labels.length === 0) return null;

    const options = monitor_base_options();
    options.scales.x.stacked = true;
    options.scales.y.stacked = true;

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: data.labels,
            datasets: (data.states ?? []).map(state => ({
                label: state.label,
                data: state.counts ?? [],
                backgroundColor: job_state_color(state.state),
                stack: 'jobs',
                borderWidth: 0
            }))
        },
        options: options
    });
}

function monitor_make_trends_chart(data) {
    const canvas = document.getElementById('monitor-trends-chart');
    if (!canvas || !window.Chart || !data || data.labels.length === 0) return null;

    const options = monitor_base_options();
    options.scales.y1 = {
        beginAtZero: true,
        position: 'right',
        title: { display: true, text: 'AI wait s/call', color: monitor_chart_text_color() },
        grid: { drawOnChartArea: false },
        ticks: { color: monitor_chart_text_color() }
    };
    options.scales.y.title = { display: true, text: 'jobs', color: monitor_chart_text_color() };

    return new Chart(canvas, {
        type: 'line',
        data: {
            labels: data.labels,
            datasets: [
                {
                    label: 'jobs per run',
                    data: data.jobs ?? [],
                    borderColor: '#0dcaf0',
                    backgroundColor: '#0dcaf0',
                    tension: 0.25,
                    pointRadius: 2,
                    borderWidth: 2
                },
                {
                    label: 'avg AI wait per call',
                    data: data.waitSeconds ?? [],
                    borderColor: '#ffc107',
                    backgroundColor: '#ffc107',
                    tension: 0.25,
                    pointRadius: 2,
                    borderWidth: 2,
                    yAxisID: 'y1'
                }
            ]
        },
        options: options
    });
}

function initMonitorCharts() {
    if (monitor_stages_chart) {
        monitor_stages_chart.destroy();
        monitor_stages_chart = null;
    }

    if (monitor_trends_chart) {
        monitor_trends_chart.destroy();
        monitor_trends_chart = null;
    }

    monitor_stages_chart = monitor_make_stages_chart(
        monitor_read_data('monitor-stages-data'));

    monitor_trends_chart = monitor_make_trends_chart(
        monitor_read_data('monitor-trends-data'));
}

window.initMonitorCharts = initMonitorCharts;

if (monitor_page_body)
    new MutationObserver(() => initMonitorCharts())
        .observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });
