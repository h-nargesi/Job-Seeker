const DONUT_PALETTE = ['#0d6efd', '#198754', '#ffc107', '#dc3545', '#6f42c1', '#0dcaf0', '#fd7e14', '#6c757d'];

const VERDICT_COLORS = {
    StrongMatch: '#198754',
    Match: '#0d6efd',
    Possible: '#ffc107',
    NoMatch: '#fd7e14',
    Error: '#dc3545'
};

const passmark_plugin = {
    id: 'passmark',
    afterDraw(chart) {
        const mark = chart.options.plugins.passmark?.value;
        if (mark == null) return;

        const bin = Math.min(9, Math.max(0, Math.floor(mark / 10)));
        const x = chart.scales.x.getPixelForValue(bin);
        if (!Number.isFinite(x)) return;

        const ctx = chart.ctx;
        ctx.save();
        ctx.strokeStyle = '#dc3545';
        ctx.setLineDash([5, 4]);
        ctx.beginPath();
        ctx.moveTo(x, chart.chartArea.top);
        ctx.lineTo(x, chart.chartArea.bottom);
        ctx.stroke();
        ctx.restore();
    }
};

let score_regex_chart = null;
let score_ai_chart = null;
let aging_chart = null;
let disposition_chart = null;
let competitiveness_chart = null;
const donut_charts = {};

function make_histogram(canvasId, withPassmark) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) return null;

    const options = base_options();
    options.plugins.legend.display = false;

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: [],
            datasets: [{ label: 'Jobs', data: [], backgroundColor: '#0dcaf0', borderWidth: 0 }]
        },
        options: options,
        plugins: withPassmark ? [passmark_plugin] : []
    });
}

function make_donut(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) return null;

    return new Chart(canvas, {
        type: 'doughnut',
        data: { labels: [], datasets: [{ data: [], backgroundColor: [], borderWidth: 0 }] },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'bottom', labels: { color: chart_text_color(), boxWidth: 10, boxHeight: 10 } } }
        }
    });
}

function make_bucket_bar(canvasId, color) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) return null;

    const options = base_options();
    options.plugins.legend.display = false;

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: [],
            datasets: [{ label: 'Jobs', data: [], backgroundColor: color, borderWidth: 0 }]
        },
        options: options
    });
}

function make_competitiveness_chart() {
    const canvas = document.getElementById('competitiveness-chart');
    if (!canvas || !window.Chart) return null;

    const options = base_options();
    options.scales.y.max = 100;
    options.scales.y.title = { display: true, text: '% passed', color: chart_text_color() };
    options.scales.y1 = {
        beginAtZero: true,
        position: 'right',
        grid: { drawOnChartArea: false },
        ticks: { color: chart_text_color() },
        title: { display: true, text: 'avg effective score', color: chart_text_color() }
    };
    options.plugins.tooltip = {
        callbacks: {
            afterBody: items => {
                const row = last_stats_full?.competitiveness?.[items[0]?.dataIndex];
                return row ? [`Evaluated: ${row.evaluated}`, `Passed: ${row.passed}`] : [];
            }
        }
    };

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: [],
            datasets: [
                { label: '% passed', data: [], backgroundColor: '#ffc107', borderWidth: 0, yAxisID: 'y' },
                { label: 'Avg effective score', data: [], backgroundColor: '#0dcaf0', borderWidth: 0, yAxisID: 'y1' }
            ]
        },
        options: options
    });
}

function render_histogram(chart, labels, bins) {
    chart.data.labels = labels;
    chart.data.datasets[0].data = bins;
    chart.update();
}

function render_donut(canvasId, donut, colors) {
    if (!donut || !donut.slices || donut.slices.length === 0) return;

    donut_charts[canvasId] = donut_charts[canvasId] || make_donut(canvasId);
    const chart = donut_charts[canvasId];
    if (!chart) return;

    chart.data.labels = donut.slices.map(slice => slice.label);
    chart.data.datasets[0].data = donut.slices.map(slice => slice.jobs);
    chart.data.datasets[0].backgroundColor = donut.slices.map((slice, index) =>
        colors[slice.label] ?? DONUT_PALETTE[index % DONUT_PALETTE.length]);
    chart.update();
}

function render_analysis(data) {
    const histograms = data.scoreHistograms;
    if (histograms) {
        score_regex_chart = score_regex_chart || make_histogram('score-regex-chart', false);
        if (score_regex_chart) render_histogram(score_regex_chart, histograms.labels, histograms.regexBins);

        score_ai_chart = score_ai_chart || make_histogram('score-ai-chart', true);
        if (score_ai_chart) {
            score_ai_chart.options.plugins.passmark = { value: histograms.aiPassmark };
            render_histogram(score_ai_chart, histograms.labels, histograms.aiBins);
        }

        set_caption('score-regex-caption', `N: ${histograms.regexJobs}`);
        set_caption('score-ai-caption', `N: ${histograms.aiJobs} — passmark ${histograms.aiPassmark}`);
    }

    (data.aiDonuts ?? []).forEach(donut => render_donut(`donut-${donut.key}`, donut, {}));
    render_donut('verdict-chart', data.aiVerdictDonut, VERDICT_COLORS);

    const aging = data.attentionAging;
    if (aging) {
        aging_chart = aging_chart || make_bucket_bar('aging-chart', '#ffc107');
        if (aging_chart && aging.total > 0) {
            render_histogram(aging_chart, aging.buckets.map(bucket => bucket.label),
                aging.buckets.map(bucket => bucket.jobs));
        }
    }

    const disposition = data.disposition;
    if (disposition) {
        disposition_chart = disposition_chart || make_bucket_bar('disposition-chart', '#0d6efd');
        if (disposition_chart && disposition.jobs > 0) {
            render_histogram(disposition_chart, disposition.buckets.map(bucket => bucket.label),
                disposition.buckets.map(bucket => bucket.jobs));
        }

        set_caption('disposition-caption',
            `median ${disposition.medianDays ?? '—'} / P90 ${disposition.p90Days ?? '—'} d (N: ${disposition.jobs})`);
    }

    const competitiveness = data.competitiveness;
    if (competitiveness && competitiveness.length > 0) {
        competitiveness_chart = competitiveness_chart || make_competitiveness_chart();
        if (competitiveness_chart) {
            competitiveness_chart.data.labels = competitiveness.map(row => row.bucket);
            competitiveness_chart.data.datasets[0].data = competitiveness.map(row => row.passRate);
            competitiveness_chart.data.datasets[1].data = competitiveness.map(row => row.avgEffectiveScore);
            competitiveness_chart.update();
        }
    }
}

function set_caption(elementId, text) {
    const element = document.getElementById(elementId);
    if (element) element.textContent = text;
}

function rebuild_analysis_charts() {
    [score_regex_chart, score_ai_chart, aging_chart, disposition_chart, competitiveness_chart]
        .forEach(chart => chart?.destroy());

    score_regex_chart = null;
    score_ai_chart = null;
    aging_chart = null;
    disposition_chart = null;
    competitiveness_chart = null;

    Object.values(donut_charts).forEach(chart => chart.destroy());
    Object.keys(donut_charts).forEach(key => delete donut_charts[key]);
}
