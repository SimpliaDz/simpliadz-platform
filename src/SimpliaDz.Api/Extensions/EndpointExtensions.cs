using SimpliaDz.Api.Endpoints.Accounting;
using SimpliaDz.Api.Endpoints.Audit;
using SimpliaDz.Api.Endpoints.Crm;
using SimpliaDz.Api.Endpoints.Identity;
using SimpliaDz.Api.Endpoints.Inventory;
using SimpliaDz.Api.Endpoints.Organization;
using SimpliaDz.Api.Endpoints.Purchasing;
using SimpliaDz.Api.Endpoints.Reporting;
using SimpliaDz.Api.Endpoints.Sales;

namespace SimpliaDz.Api.Extensions;

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
        app.MapGoodsReceiptEndpoints();
        app.MapCustomerEndpoints();
        app.MapSaleEndpoints();
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
