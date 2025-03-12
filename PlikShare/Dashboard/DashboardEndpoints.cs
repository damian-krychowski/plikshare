using PlikShare.Core.Authorization;
using PlikShare.Core.Protobuf;
using PlikShare.Dashboard.Content;
using PlikShare.Dashboard.Content.Contracts;
using PlikShare.Users.Middleware;

namespace PlikShare.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapGet("/", GetData)
            .WithName("GetDashboardContent")
            .WithProtobufResponse();
    }

    private static GetDashboardContentResponseDto GetData(
        HttpContext httpContext,
        GetDashboardContentQuery getDashboardContentQuery)
    {
        var user = httpContext.GetUserContext();

        var result = getDashboardContentQuery.Execute(
            user: user);

        return result;
    }
}