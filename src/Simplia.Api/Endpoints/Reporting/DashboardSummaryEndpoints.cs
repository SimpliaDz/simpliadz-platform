using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Reporting.UseCases.Dashboard;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Reporting;

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
