const dashboard_chart_body = document.getElementById('dashboard-chart-body');

const DASHBOARD_CHART_REFRESH_MS = 15000;

let loading_stats_daily = false;

async function LoadStatsDaily() {
    if (loading_stats_daily || !dashboard_chart_body) return;
    loading_stats_daily = true;

    try {
        const response = await fetch('/report/statsdaily', { method: 'GET' });
        const data = await response.json();

        if (!window.Chart || !data) return;

        // chart datasets land in phase 3

    } catch (e) {
        console.error(e);
    } finally {
        loading_stats_daily = false;
    }
}

if (dashboard_chart_body)
    setInterval(LoadStatsDaily, DASHBOARD_CHART_REFRESH_MS);
