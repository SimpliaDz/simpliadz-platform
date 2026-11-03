using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Reporting.UseCases.LowStock;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Reporting;

public static class LowStockReportEndpoints
{
    public static IEndpointRouteBuilder MapLowStockReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/low-stock", async (Guid? locationId, int? page,
            int? pageSize, [FromServices] ILowStockReportService report,
            CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedPageSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (locationId == Guid.Empty)
                errors["locationId"] = ["Location must be a valid ID."];
            if (selectedPage < 1 || selectedPageSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedPageSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await report.GetAsync(locationId, selectedPage,
                selectedPageSize, ct));
        })
        .WithTags("Reports")
        .WithMetadata(new TenantRequiredMetadata())
        .WithMetadata(new ModuleEndpointMetadata("Reporting"))
        .RequireAuthorization(PermissionNames.Reports.LowStock);

        return app;
    }
}
