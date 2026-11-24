using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Reporting.UseCases.FinancialDashboard;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Reporting;

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
