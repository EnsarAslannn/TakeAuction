using System.Security.Claims;
using Hangfire.Dashboard;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Jobs;

public sealed class AdminOnlyDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => IsAdmin(context.GetHttpContext().User);

    public static bool IsAdmin(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.IsInRole(nameof(UserRole.Admin));
}
