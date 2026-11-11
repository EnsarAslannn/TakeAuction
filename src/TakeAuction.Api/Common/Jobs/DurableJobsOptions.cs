namespace TakeAuction.Api.Common.Jobs;

public sealed class DurableJobsOptions
{
    public const string SectionName = "DurableJobs";
    public bool Enabled { get; set; }
    public Uri? SchedulerUrl { get; set; }
    public string Secret { get; set; } = "";
}
