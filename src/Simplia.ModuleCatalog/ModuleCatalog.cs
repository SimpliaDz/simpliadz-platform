using Simplia.Accounting.Infrastructure;
using Simplia.Audit.Infrastructure;
using Simplia.Crm.Infrastructure;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.Identity.Infrastructure;
using Simplia.Inventory.Infrastructure;
using Simplia.Notifications.Infrastructure;
using Simplia.Organization.Infrastructure;
using Simplia.Purchasing.Infrastructure;
using Simplia.Reporting.Infrastructure;
using Simplia.Sales.Infrastructure;

namespace Simplia.ModuleCatalog;

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
