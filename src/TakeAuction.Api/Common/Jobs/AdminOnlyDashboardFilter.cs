using System.Security.Claims;
using Hangfire.Dashboard;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Jobs;

/// <summary>
/// The dashboard can requeue and delete jobs, so it is gated on the signed-in principal
/// rather than on the environment alone. Without this, the day somebody drops the
/// Development check the dashboard is open to anybody who knows the path.
/// </summary>
public sealed class AdminOnlyDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => IsAdmin(context.GetHttpContext().User);

    public static bool IsAdmin(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.IsInRole(nameof(UserRole.Admin));
}
