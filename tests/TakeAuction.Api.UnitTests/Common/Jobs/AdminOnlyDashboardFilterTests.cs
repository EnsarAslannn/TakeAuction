using System.Security.Claims;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.UnitTests.Common.Jobs;

public sealed class AdminOnlyDashboardFilterTests
{
    [Fact]
    public void An_anonymous_visitor_is_turned_away()
    {
        Assert.False(AdminOnlyDashboardFilter.IsAdmin(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Theory]
    [InlineData(nameof(UserRole.Bidder))]
    [InlineData(nameof(UserRole.Seller))]
    public void A_signed_in_user_without_the_admin_role_is_turned_away(string role)
    {
        Assert.False(AdminOnlyDashboardFilter.IsAdmin(PrincipalWith(role)));
    }

    [Fact]
    public void An_admin_gets_in()
    {
        Assert.True(AdminOnlyDashboardFilter.IsAdmin(PrincipalWith(nameof(UserRole.Admin))));
    }

    [Fact]
    public void An_unauthenticated_principal_carrying_the_role_claim_is_still_turned_away()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, nameof(UserRole.Admin))]);

        Assert.False(identity.IsAuthenticated);
        Assert.False(AdminOnlyDashboardFilter.IsAdmin(new ClaimsPrincipal(identity)));
    }

    private static ClaimsPrincipal PrincipalWith(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "Test"));
}
