using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Reporting.UseCases.ProductPerformance;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Reporting;

public static class ProductPerformanceReportEndpoints
{
    public static IEndpointRouteBuilder MapProductPerformanceReportEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports/products")
            .WithTags("Reports")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Reporting"));

        group.MapGet("/performance", async (DateOnly? fromDate, DateOnly? toDate,
            string? rankBy, int? page, int? pageSize,
            [FromServices] IProductPerformanceReportService report,
            CancellationToken ct) =>
        {
            var errors = Validate(fromDate, toDate, page ?? 1, pageSize ?? 50);
            var selectedRank = rankBy?.Trim();
            if (selectedRank is not null &&
                !Enum.GetNames<ProductPerformanceRankBy>().Any(x =>
                    string.Equals(x, selectedRank, StringComparison.OrdinalIgnoreCase)))
                errors["rankBy"] = ["Rank by must be Quantity or Revenue."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var ranking = selectedRank is null
                ? ProductPerformanceRankBy.Quantity
                : Enum.Parse<ProductPerformanceRankBy>(selectedRank, true);
            return Results.Ok(await report.RankProductsAsync(fromDate!.Value,
                toDate!.Value, ranking, page ?? 1, pageSize ?? 50, ct));
        }).RequireAuthorization(PermissionNames.Reports.ProductPerformance);

        group.MapGet("/{productId:guid}/variants", async (Guid productId,
            DateOnly? fromDate, DateOnly? toDate, int? page, int? pageSize,
            [FromServices] IProductPerformanceReportService report,
            CancellationToken ct) =>
        {
            var errors = Validate(fromDate, toDate, page ?? 1, pageSize ?? 50);
            if (productId == Guid.Empty) errors["productId"] = ["Product is required."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await report.GetVariantsAsync(productId,
                fromDate!.Value, toDate!.Value, page ?? 1, pageSize ?? 50, ct));
        }).RequireAuthorization(PermissionNames.Reports.ProductPerformance);

        return app;
    }

    private static Dictionary<string, string[]> Validate(DateOnly? fromDate,
        DateOnly? toDate, int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (fromDate is null || fromDate == default(DateOnly))
            errors["fromDate"] = ["From date is required."];
        if (toDate is null || toDate == default(DateOnly))
            errors["toDate"] = ["To date is required."];
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            errors["dateRange"] = ["From date cannot be after to date."];
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
        return errors;
    }
}
