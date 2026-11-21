using SimpliaDz.Accounting.Infrastructure;
using SimpliaDz.Audit.Infrastructure;
using SimpliaDz.Crm.Infrastructure;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.Identity.Infrastructure;
using SimpliaDz.Inventory.Infrastructure;
using SimpliaDz.Notifications.Infrastructure;
using SimpliaDz.Organization.Infrastructure;
using SimpliaDz.Purchasing.Infrastructure;
using SimpliaDz.Reporting.Infrastructure;
using SimpliaDz.Sales.Infrastructure;

namespace SimpliaDz.ModuleCatalog;

/// <summary>
/// The authoritative list of modules installed in this deployment.
/// Hosts decide which of these modules to activate from their tenant configuration.
/// </summary>
public static class ModuleCatalog
{
    public static IReadOnlyList<IModuleInstaller> GetModules() =>
    [
        new IdentityModuleInstaller(),
        new InventoryModuleInstaller(),
        new OrganizationModuleInstaller(),
        new SalesModuleInstaller(),
        new PurchasingModuleInstaller(),
        new AccountingModuleInstaller(),
        new NotificationsModuleInstaller(),
        new ReportingModuleInstaller(),
        new AuditModuleInstaller(),
        new CrmModuleInstaller()
    ];
}
