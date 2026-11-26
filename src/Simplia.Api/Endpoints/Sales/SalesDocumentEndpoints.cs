using Simplia.Api.Modules;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Sales;

public static class SalesDocumentEndpoints
{
    public static IEndpointRouteBuilder MapSalesDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sales/{saleId:guid}/documents").WithTags("Sales documents")
            .WithMetadata(new TenantRequiredMetadata()).WithMetadata(new ModuleEndpointMetadata("Sales"))
            .RequireAuthorization(PermissionNames.Sales.View);
        group.MapGet("/pro-forma.pdf", async (Guid saleId, SalesPdfDocumentService documents, CancellationToken ct) =>
        {
            var pdf = await documents.ProFormaAsync(saleId, ct);
            return Results.File(pdf.Content, "application/pdf", pdf.FileName);
        });
        group.MapGet("/receipt.pdf", async (Guid saleId, SalesPdfDocumentService documents, CancellationToken ct) =>
        {
            var pdf = await documents.ReceiptAsync(saleId, ct);
            return Results.File(pdf.Content, "application/pdf", pdf.FileName);
        });
        return app;
    }
}
