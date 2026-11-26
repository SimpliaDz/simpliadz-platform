using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Reporting.UseCases.FinancialDashboard;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Reporting;

public static class FinancialDashboardEndpoints
{
    public static IEndpointRouteBuilder MapFinancialDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/financial-dashboard", async (DateOnly? fromDate, DateOnly? toDate,
            Guid? locationId, [FromServices] IFinancialDashboardService report, CancellationToken ct) =>
        {
            if (fromDate is null || toDate is null || fromDate == default || toDate == default || fromDate > toDate)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["dateRange"] = ["A valid from and to date is required."] });
            return Results.Ok(await report.GetAsync(fromDate.Value, toDate.Value, locationId, ct));
        }).WithTags("Reports").WithMetadata(new TenantRequiredMetadata())
          .WithMetadata(new ModuleEndpointMetadata("Reporting"))
          .RequireAuthorization(PermissionNames.Reports.SalesSummary);
        return app;
    }
}
