using Simplia.Api.Endpoints.Accounting;
using Simplia.Api.Endpoints.Audit;
using Simplia.Api.Endpoints.Crm;
using Simplia.Api.Endpoints.Identity;
using Simplia.Api.Endpoints.Inventory;
using Simplia.Api.Endpoints.Organization;
using Simplia.Api.Endpoints.Purchasing;
using Simplia.Api.Endpoints.Reporting;
using Simplia.Api.Endpoints.Sales;

namespace Simplia.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new { status = "ok" }));
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        app.MapRoleEndpoints();
        app.MapPermissionEndpoints();
        app.MapProductEndpoints();
        app.MapCategoryEndpoints();
        app.MapBrandEndpoints();
        app.MapAttributeEndpoints();
        app.MapVariantEndpoints();
        app.MapBarcodeLabelEndpoints();
        app.MapInventoryDocumentEndpoints();
        app.MapInventoryWarningEndpoints();
        app.MapPriceListEndpoints();
        app.MapLocationEndpoints();
        app.MapStockEndpoints();
        app.MapInventoryPolicyEndpoints();
        app.MapStockTransferEndpoints();
        app.MapInventoryCountEndpoints();
        app.MapOrganizationEndpoints();
        app.MapWorkSessionEndpoints();
        app.MapSupplierEndpoints();
        app.MapPurchaseOrderEndpoints();
        app.MapSupplierPaymentEndpoints();
        app.MapGoodsReceiptEndpoints();
        app.MapCustomerEndpoints();
        app.MapSaleEndpoints();
        app.MapPromotionEndpoints();
        app.MapSalesDocumentEndpoints();
        app.MapPosEndpoints();
        app.MapPosProductLookupEndpoints();
        app.MapPaymentEndpoints();
        app.MapSaleReturnEndpoints();
        app.MapExpenseCategoryEndpoints();
        app.MapExpenseEndpoints();
        app.MapSalesSummaryReportEndpoints();
        app.MapFinancialDashboardEndpoints();
        app.MapProductPerformanceReportEndpoints();
        app.MapInventoryReportEndpoints();
        app.MapLowStockReportEndpoints();
        app.MapDashboardSummaryEndpoints();
        app.MapCashRegisterEndpoints();
        app.MapCashSessionEndpoints();
        app.MapAuditEndpoints();
        app.MapCrmEndpoints();

        return app;
    }
}
