using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Reporting.UseCases.Dashboard;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Reporting;

public static class DashboardSummaryEndpoints
{
    public static IEndpointRouteBuilder MapDashboardSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/dashboard/today", async (
            [FromServices] IDashboardSummaryService dashboard, CancellationToken ct) =>
            Results.Ok(await dashboard.GetTodayAsync(ct)))
            .WithTags("Reports")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Reporting"))
            .RequireAuthorization(PermissionNames.Reports.Dashboard);
        return app;
    }
}
