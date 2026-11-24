using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Reporting.UseCases.SalesSummary;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Reporting;

public static class SalesSummaryReportEndpoints
{
    public static IEndpointRouteBuilder MapSalesSummaryReportEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/sales/summary", async (DateOnly? fromDate,
            DateOnly? toDate, Guid? locationId, [FromServices] ISalesSummaryReportService report,
            CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (fromDate is null || fromDate == default(DateOnly))
                errors["fromDate"] = ["From date is required."];
            if (toDate is null || toDate == default(DateOnly))
                errors["toDate"] = ["To date is required."];
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                errors["dateRange"] = ["From date cannot be after to date."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            if (locationId == Guid.Empty) errors["locationId"] = ["Location ID must be valid."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await report.GetAsync(fromDate!.Value, toDate!.Value, locationId, ct));
        }).WithTags("Reports")
          .WithMetadata(new TenantRequiredMetadata())
          .WithMetadata(new ModuleEndpointMetadata("Reporting"))
          .RequireAuthorization(PermissionNames.Reports.SalesSummary);
        return app;
    }
}
