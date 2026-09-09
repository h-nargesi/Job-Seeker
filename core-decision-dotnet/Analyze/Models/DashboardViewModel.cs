namespace Photon.JobSeeker;

public record DashboardViewModel(List<TrendReportItem> Trends, List<JobListItem> Jobs,
    AgencyDashboardItem[] Agencies);
