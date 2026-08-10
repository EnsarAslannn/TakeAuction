using Hangfire;

namespace TakeAuction.Api.Common.Jobs;

public interface IRecurringJobRegistration
{
    void Register(IRecurringJobManager manager);
}
