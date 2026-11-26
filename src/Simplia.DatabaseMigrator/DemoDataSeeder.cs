using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Simplia.Accounting.Core;
using Simplia.Accounting.Infrastructure.Data;
using Simplia.Audit.Core;
using Simplia.Audit.Infrastructure.Data;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Crm.Core.Entities;
using Simplia.Crm.Infrastructure.Data;
using Simplia.Identity.Core.Entities;
using Simplia.Identity.Infrastructure.Data;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.Infrastructure.Data;
using Simplia.Organization.Core.Entities;
using Simplia.Organization.Infrastructure.Data;
using Simplia.Purchasing.Core.Entities;
using Simplia.Purchasing.Infrastructure.Data;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.Infrastructure.Data;
using Simplia.SharedKernel.Authorization;
using OrganizationProfile = Simplia.Organization.Core.Entities.Organization;

internal static class DemoDataSeeder
{
    private const string Marker = "[SIMPLIA-DEMO-SEED]";
    private const string Actor = "demo-seed";
    private const string DemoPassword = "Demo123!";

    public static async Task SeedAsync(IServiceProvider services, ITenantContext tenant,
        CancellationToken ct)
    {
        if (!tenant.IsModuleEnabled("Identity") || !tenant.IsModuleEnabled("Inventory"))
            throw new InvalidOperationException("Demo data requires Identity and Inventory modules.");

        var users = await SeedUsersAsync(services, ct);
        var inventory = services.GetRequiredService<InventoryDbContext>();
        var mainId = await inventory.Locations.Where(x => x.Code == "MAIN")
            .Select(x => x.Id).SingleAsync(ct);
        var warehouseId = await inventory.Locations.Where(x => x.Code == "WH")
            .Select(x => x.Id).SingleAsync(ct);
        var variants = await inventory.ProductVariants.ToListAsync(ct);
        Guid Variant(string sku) => variants.Single(x => x.Sku.Value == sku).Id;

        await SeedInventoryAsync(inventory, mainId, warehouseId,
            Variant("NK-HOOD-GRY-L"), Variant("TSHIRT-BLK-M"), ct);

        if (tenant.IsModuleEnabled("Organization"))
            await SeedOrganizationAsync(services.GetRequiredService<OrganizationDbContext>(),
                users.CashierId, ct);

        if (tenant.IsModuleEnabled("Purchasing"))
        {
            await SeedPurchasingAsync(services.GetRequiredService<PurchasingDbContext>(),
                warehouseId, Variant("TSHIRT-BLK-M"), Variant("NK-HOOD-BLK-M"), ct);
            await SeedGoodsReceiptAsync(tenant.ConnectionString, warehouseId, ct);
        }

        Guid? completedSaleId = null;
        if (tenant.IsModuleEnabled("Sales"))
        {
            completedSaleId = await SeedSalesAsync(services.GetRequiredService<SalesDbContext>(),
                tenant.ConnectionString, mainId, Variant("TSHIRT-BLK-M"),
                users.CashierId, inventory, ct);
            var secondShop = await EnsureHistoryShopAsync(inventory, ct);
            await SeedSalesHistoryAsync(tenant.ConnectionString, [mainId, secondShop], users.CashierId, ct);
            await SeedPromotionsAsync(services.GetRequiredService<SalesDbContext>(), inventory,
                Variant("TSHIRT-BLK-M"), ct);
            if (tenant.IsModuleEnabled("Accounting"))
                await SeedHistoricalExpensesAsync(services.GetRequiredService<AccountingDbContext>(),
                    [mainId, secondShop], users.ManagerId, ct);
        }

        if (tenant.IsModuleEnabled("Crm") && tenant.IsModuleEnabled("Sales"))
            await SeedCrmAsync(services.GetRequiredService<CrmDbContext>(),
                services.GetRequiredService<SalesDbContext>(), ct);

        if (tenant.IsModuleEnabled("Accounting"))
            await SeedAccountingAsync(services.GetRequiredService<AccountingDbContext>(),
                mainId, users.ManagerId, ct);

        if (tenant.IsModuleEnabled("Audit"))
            await SeedAuditAsync(services.GetRequiredService<AuditDbContext>(),
                users.ManagerId, completedSaleId ?? mainId, ct);

        Console.WriteLine($"Demo users: cashier.demo@local.test, manager.demo@local.test, hr.demo@local.test (initial password: {DemoPassword}).");
        Console.WriteLine("Existing demo users and business records are preserved on later runs.");
    }

    private static async Task<(Guid CashierId, Guid ManagerId)> SeedUsersAsync(
        IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        async Task<Guid> EnsureUser(string roleName, string email, string firstName,
            params string[] permissionNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new AppRole { Name = roleName };
                var created = await roleManager.CreateAsync(role);
                if (!created.Succeeded)
                    throw new InvalidOperationException($"Cannot create demo role {roleName}: {string.Join(", ", created.Errors.Select(x => x.Description))}");
            }

            var assigned = await db.RolePermissions.Where(x => x.RoleId == role.Id)
                .Select(x => x.PermissionId).ToListAsync(ct);
            var wanted = await db.Permissions.Where(x => permissionNames.Contains(x.Name))
                .Select(x => x.Id).ToListAsync(ct);
            if (wanted.Count != permissionNames.Distinct().Count())
                throw new InvalidOperationException($"Some permissions for demo role {roleName} were not seeded.");
            foreach (var permissionId in wanted.Except(assigned))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
            await db.SaveChangesAsync(ct);

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new AppUser
                {
                    UserName = email, Email = email, FirstName = firstName,
                    LastName = "Demo", EmailConfirmed = true, IsActive = true
                };
                var created = await userManager.CreateAsync(user, DemoPassword);
                if (!created.Succeeded)
                    throw new InvalidOperationException($"Cannot create demo user {email}: {string.Join(", ", created.Errors.Select(x => x.Description))}");
            }
            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                var assignedRole = await userManager.AddToRoleAsync(user, roleName);
                if (!assignedRole.Succeeded)
                    throw new InvalidOperationException($"Cannot assign demo role {roleName}.");
            }
            return user.Id;
        }

        var cashier = await EnsureUser("Demo Cashier", "cashier.demo@local.test", "Cashier",
            PermissionNames.Products.View, PermissionNames.Variants.View,
            PermissionNames.Stock.View, PermissionNames.Locations.View,
            PermissionNames.PriceLists.View,
            PermissionNames.Sales.View, PermissionNames.Sales.Manage,
            PermissionNames.SalePayments.Manage, PermissionNames.Customers.View,
            PermissionNames.Customers.Manage, PermissionNames.CashSessions.View,
            PermissionNames.CashSessions.Manage, PermissionNames.Crm.View,
            PermissionNames.Crm.Redeem);
        var businessPermissions = PermissionNames.Catalog
            .Where(x => x.Category is not ("Users" or "Roles" or "Permissions"))
            .Select(x => x.Name).ToArray();
        var manager = await EnsureUser("Demo Manager", "manager.demo@local.test", "Manager",
            businessPermissions);
        await EnsureUser("Demo HR", "hr.demo@local.test", "HR",
            PermissionNames.Users.View, PermissionNames.Users.Create,
            PermissionNames.Users.Edit, PermissionNames.WorkSessions.View,
            PermissionNames.WorkSessions.Manage, PermissionNames.WorkSessions.Report);
        return (cashier, manager);
    }

    private static async Task SeedOrganizationAsync(OrganizationDbContext db,
        Guid cashierId, CancellationToken ct)
    {
        if (!await db.Organizations.AnyAsync(ct))
            db.Organizations.Add(new OrganizationProfile("Simplia Demo Shop",
                "Simplia Demo Shop", "+213 555 010 100", "demo@local.test",
                "Algiers, Algeria", "DZD", "fr", true, "Africa/Algiers"));
        if (!await db.WorkSessions.AnyAsync(x => x.UserId == cashierId, ct))
        {
            var start = DateTimeOffset.UtcNow.AddDays(-1).Date.AddHours(9);
            var session = new WorkSession(cashierId, new DateTimeOffset(start, TimeSpan.Zero));
            session.ClockOut(cashierId, new DateTimeOffset(start.AddHours(8), TimeSpan.Zero));
            db.WorkSessions.Add(session);
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedInventoryAsync(InventoryDbContext db, Guid mainId,
        Guid warehouseId, Guid agedVariantId, Guid transferVariantId, CancellationToken ct)
    {
        if (!await db.InventoryPolicies.AnyAsync(x => x.ProductVariantId == agedVariantId &&
            x.LocationId == mainId, ct))
            db.InventoryPolicies.Add(new InventoryPolicy(agedVariantId, mainId, 5, 15));

        var addedAgedStock = !await db.StockBalances.AnyAsync(x =>
            x.VariantId == agedVariantId && x.LocationId == mainId, ct);
        if (addedAgedStock)
        {
            var balance = new StockBalance(agedVariantId, mainId);
            balance.ApplyDelta(3);
            db.StockBalances.Add(balance);
            var movement = new StockMovement(agedVariantId, mainId,
                StockMovementType.OpeningBalance, 3, Actor, reference: "DEMO",
                reason: "Demo ageing and low-stock example");
            db.StockMovements.Add(movement);
            db.Entry(movement).Property(x => x.OccurredAt).CurrentValue =
                DateTimeOffset.UtcNow.AddDays(-120);

        }

        var settings = await db.InventoryWarningSettings.SingleAsync(ct);
        if (!settings.Enabled && settings.LastRunAt is null &&
            (addedAgedStock || await db.StockMovements.AnyAsync(x =>
                x.VariantId == agedVariantId && x.LocationId == mainId &&
                x.Reference == "DEMO", ct)))
            settings.Configure(true, true, true, 60, 90);

        if (!await db.StockTransfers.AnyAsync(x => x.CreatedBy == Actor, ct))
        {
            var transfer = new StockTransfer(mainId, warehouseId, Actor);
            transfer.AddLine(transferVariantId, 2);
            db.StockTransfers.Add(transfer);
        }
        if (!await db.InventoryCounts.AnyAsync(x => x.CreatedBy == Actor, ct))
        {
            var expected = await db.StockBalances.Where(x => x.VariantId == transferVariantId &&
                x.LocationId == mainId).Select(x => x.QuantityOnHand).SingleAsync(ct);
            var count = new InventoryCount(mainId, Actor);
            count.AddLine(transferVariantId, expected);
            db.InventoryCounts.Add(count);
        }
        if (!await db.StockTransfers.AnyAsync(x => x.CreatedBy == "demo-cancelled-transfer", ct))
        {
            var transfer = new StockTransfer(mainId, warehouseId, "demo-cancelled-transfer");
            transfer.AddLine(transferVariantId, 1);
            transfer.Cancel();
            db.StockTransfers.Add(transfer);
        }
        if (!await db.InventoryCounts.AnyAsync(x => x.CreatedBy == "demo-count-difference", ct))
        {
            var expected = await db.StockBalances.Where(x => x.VariantId == transferVariantId &&
                x.LocationId == mainId).Select(x => x.QuantityOnHand).SingleAsync(ct);
            var count = new InventoryCount(mainId, "demo-count-difference");
            count.AddLine(transferVariantId, expected);
            count.SetCountedQuantity(transferVariantId, expected + 1);
            db.InventoryCounts.Add(count);
        }
        if (!await db.BarcodeLabelTemplates.AnyAsync(x => x.Name == "Demo Archived Label", ct))
        {
            var template = new BarcodeLabelTemplate("Demo Archived Label", 210, 297, 65, 30, 8, 3, true, true);
            template.Deactivate();
            db.BarcodeLabelTemplates.Add(template);
        }
        if (!await db.InventoryDocumentTemplates.AnyAsync(x => x.Name == "Demo Archived Stock List", ct))
        {
            var template = new InventoryDocumentTemplate("Demo Archived Stock List", InventoryDocumentType.StockList,
                "Demo stock levels", ["Product", "Sku", "Quantity"], "A4", false, null);
            template.Deactivate();
            db.InventoryDocumentTemplates.Add(template);
        }
        if (!await db.ProductAttributes.AnyAsync(x => x.Name == "Demo Packaging", ct))
        {
            var attribute = new ProductAttribute("Demo Packaging");
            attribute.AddValue("Single pack", 1);
            attribute.AddValue("Gift box", 2);
            db.ProductAttributes.Add(attribute);
        }
        await db.SaveChangesAsync(ct);
        await SeedUnlabelledProductAsync(db, ct);
    }

    private static async Task SeedUnlabelledProductAsync(InventoryDbContext db,
        CancellationToken ct)
    {
        var product = await db.Products.Include(x => x.Variants)
            .SingleOrDefaultAsync(x => x.Code == "DEMO-CAP", ct);
        if (product is null)
        {
            product = new Product("Demo Unlabelled Cap", "DEMO-CAP",
                "Use its variant to test internal barcode generation.");
            product.AddVariant("DEMO-CAP-BLK", 1200m, 800m, null, "Black", null);
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
        }
        var variant = product.Variants.Single(x => x.Sku.Value == "DEMO-CAP-BLK");
        var lists = await db.PriceLists.Include(x => x.Items)
            .Where(x => x.CurrencyCode == "DZD" &&
                (x.Name == "Retail" || x.Name == "Wholesale")).ToListAsync(ct);
        foreach (var list in lists)
        {
            if (list.Items.Any(x => x.ProductVariantId == variant.Id)) continue;
            list.SetPrice(variant.Id, list.Name == "Retail" ? 1200m : 800m);
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedPurchasingAsync(PurchasingDbContext db, Guid warehouseId,
        Guid shirtVariantId, Guid hoodieVariantId, CancellationToken ct)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Name == "Demo Textiles Supplier", ct);
        if (supplier is null)
        {
            supplier = new Supplier("Demo Textiles Supplier", "+213 555 010 200",
                "supplier.demo@local.test", "Algiers, Algeria", Marker);
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(ct);
        }
        if (!await db.PurchaseOrders.AnyAsync(x => x.Notes == Marker + " Draft PO", ct))
        {
            var draft = new PurchaseOrder(supplier.Id, warehouseId, Actor, Marker + " Draft PO");
            draft.AddLine(shirtVariantId, 12, 900m);
            db.PurchaseOrders.Add(draft);
        }
        if (!await db.PurchaseOrders.AnyAsync(x => x.Notes == Marker + " Ready to receive", ct))
        {
            var ordered = new PurchaseOrder(supplier.Id, warehouseId, Actor,
                Marker + " Ready to receive");
            ordered.AddLine(shirtVariantId, 10, 900m);
            ordered.AddLine(hoodieVariantId, 5, 7200m);
            ordered.PlaceOrder();
            db.PurchaseOrders.Add(ordered);
        }
        if (!await db.PurchaseOrders.AnyAsync(x => x.Notes == Marker + " Partial receipt", ct))
        {
            var partial = new PurchaseOrder(supplier.Id, warehouseId, Actor,
                Marker + " Partial receipt");
            partial.AddLine(shirtVariantId, 10, 900m);
            partial.AddLine(hoodieVariantId, 5, 7200m);
            partial.PlaceOrder();
            db.PurchaseOrders.Add(partial);
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedGoodsReceiptAsync(string connectionString,
        Guid warehouseId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var purchaseOptions = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseSqlServer(connection).Options;
        var stockOptions = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connection).Options;
        await using var purchasing = new PurchasingDbContext(purchaseOptions);
        await using var stock = new InventoryDbContext(stockOptions);
        var order = await purchasing.PurchaseOrders.Include(x => x.Lines)
            .SingleAsync(x => x.Notes == Marker + " Partial receipt", ct);
        if (order.Status != PurchaseOrderStatus.Ordered ||
            await purchasing.GoodsReceipts.AnyAsync(x => x.PurchaseOrderId == order.Id, ct))
            return;

        var line = order.Lines.Single(x => x.UnitCost == 900m);
        await using var transaction = await purchasing.Database.BeginTransactionAsync(ct);
        await stock.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        var receipt = new GoodsReceipt(Guid.NewGuid(), order.Id, warehouseId, Actor,
            [new GoodsReceiptLineItem(line.Id, line.ProductVariantId, 4, line.UnitCost)]);
        order.RecordReceipt(line.Id, 4);
        purchasing.GoodsReceipts.Add(receipt);
        var balance = await stock.StockBalances.SingleAsync(x =>
            x.VariantId == line.ProductVariantId && x.LocationId == warehouseId, ct);
        balance.ApplyDelta(4);
        stock.StockMovements.Add(new StockMovement(line.ProductVariantId, warehouseId,
            StockMovementType.PurchaseReceipt, 4, Actor, referenceType: "GoodsReceipt",
            referenceId: receipt.Id, sourceLineNumber: 1));
        await purchasing.SaveChangesAsync(ct);
        await stock.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task SeedCrmAsync(CrmDbContext crm, SalesDbContext sales,
        CancellationToken ct)
    {
        if (!await crm.Programs.AnyAsync(ct))
            crm.Programs.Add(new LoyaltyProgram("Demo Loyalty", 100m));
        if (!await crm.Rewards.AnyAsync(x => x.Name == "Demo Gift", ct))
            crm.Rewards.Add(new LoyaltyReward("Demo Gift", 10));
        var customerId = await sales.Customers.AsNoTracking()
            .Where(x => x.Email == "sale.demo@local.test")
            .Select(x => x.Id).SingleAsync(ct);
        if (!await crm.Cards.AnyAsync(x => x.CustomerId == customerId, ct))
            crm.Cards.Add(new LoyaltyCard(customerId));
        await crm.SaveChangesAsync(ct);
    }

    private static async Task<Guid?> SeedSalesAsync(SalesDbContext db,
        string connectionString, Guid mainId, Guid shirtVariantId, Guid cashierId,
        InventoryDbContext inventory, CancellationToken ct)
    {
        var retailCustomer = await db.Customers.SingleOrDefaultAsync(x =>
            x.Email == "retail.demo@local.test", ct);
        if (retailCustomer is null)
        {
            retailCustomer = new Customer("Demo Retail Customer", "+213 555 010 300",
                "retail.demo@local.test", "Algiers", CustomerType.Retail, Marker);
            db.Customers.Add(retailCustomer);
        }
        var saleCustomer = await db.Customers.SingleOrDefaultAsync(x =>
            x.Email == "sale.demo@local.test", ct);
        if (saleCustomer is null)
        {
            saleCustomer = new Customer("Demo Sale Customer", "+213 555 010 301",
                "sale.demo@local.test", "Algiers", CustomerType.Retail, Marker);
            db.Customers.Add(saleCustomer);
        }
        if (!await db.Customers.AnyAsync(x => x.Email == "wholesale.demo@local.test", ct))
            db.Customers.Add(new Customer("Demo Wholesale Customer", null,
                "wholesale.demo@local.test", null, CustomerType.Wholesale, Marker));

        var register = await db.CashRegisters.SingleOrDefaultAsync(x =>
            x.LocationId == mainId && x.Code == "DEMO-REG-1", ct);
        if (register is null)
        {
            register = new CashRegister(mainId, "DEMO-REG-1", "Demo Front Counter");
            db.CashRegisters.Add(register);
        }
        var secondRegister = await db.CashRegisters.SingleOrDefaultAsync(x => x.LocationId == mainId &&
            x.Code == "DEMO-REG-2", ct);
        if (secondRegister is null)
        {
            secondRegister = new CashRegister(mainId, "DEMO-REG-2", "Demo Second Counter");
            db.CashRegisters.Add(secondRegister);
        }
        await db.SaveChangesAsync(ct);

        var session = await db.CashSessions.SingleOrDefaultAsync(x =>
            x.CashRegisterId == register.Id && x.Status == CashSessionStatus.Open, ct);
        if (session is null && !await db.CashSessions.AnyAsync(x => x.CashRegisterId == register.Id, ct))
        {
            session = new CashSession(register.Id, cashierId, 5000m);
            db.CashSessions.Add(session);
            await db.SaveChangesAsync(ct);
        }
        if (!await db.CashSessions.AnyAsync(x => x.CashRegisterId == secondRegister.Id &&
                x.Status == CashSessionStatus.Open, ct))
        {
            db.CashSessions.Add(new CashSession(secondRegister.Id, cashierId, 3000m));
            await db.SaveChangesAsync(ct);
        }

        var list = await inventory.PriceLists.SingleAsync(x => x.Name == "Retail" &&
            x.CurrencyCode == "DZD", ct);
        if (!await db.Sales.AnyAsync(x => x.CustomerId == retailCustomer.Id, ct))
        {
            var draft = new Sale(mainId, retailCustomer.Id, cashierId, list.Id);
            draft.AddLine(shirtVariantId, 1, 1500m, 900m);
            db.Sales.Add(draft);
            await db.SaveChangesAsync(ct);
        }

        var existingSale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(x =>
            x.CustomerId == saleCustomer.Id, ct);
        if (existingSale is not null) return existingSale.Id;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        var salesOptions = new DbContextOptionsBuilder<SalesDbContext>().UseSqlServer(connection).Options;
        var stockOptions = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlServer(connection).Options;
        await using var sales = new SalesDbContext(salesOptions);
        await using var stock = new InventoryDbContext(stockOptions);
        await using var transaction = await sales.Database.BeginTransactionAsync(ct);
        await stock.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);

        var balance = await stock.StockBalances.SingleAsync(x => x.VariantId == shirtVariantId &&
            x.LocationId == mainId, ct);
        var completed = new Sale(mainId, saleCustomer.Id, cashierId, list.Id);
        var line = completed.AddLine(shirtVariantId, 2, 1500m, 900m);
        completed.ApplyLineDiscount(line.Id, SaleDiscountType.Percentage, 10m);
        completed.ApplySaleDiscount(SaleDiscountType.FixedAmount, 100m);
        completed.Complete(cashierId);
        completed.RecordPayment(completed.Total);
        sales.Sales.Add(completed);
        sales.Payments.Add(new Payment(Guid.NewGuid(), completed.Id, completed.Total,
            PaymentMethod.Cash, cashierId, session?.Id));
        balance.ApplyDelta(-2);
        stock.StockMovements.Add(new StockMovement(shirtVariantId, mainId,
            StockMovementType.Sale, -2, cashierId.ToString(), referenceType: "Sale",
            referenceId: completed.Id, sourceLineNumber: 1));
        await sales.SaveChangesAsync(ct);
        await stock.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return completed.Id;
    }

    private static async Task<Guid> EnsureHistoryShopAsync(InventoryDbContext db, CancellationToken ct)
    {
        var shop = await db.Locations.SingleOrDefaultAsync(x => x.Code == "DEMO-EAST", ct);
        if (shop is not null) return shop.Id;
        shop = new InventoryLocation("Demo East Shop", "DEMO-EAST");
        db.Locations.Add(shop);
        var inactive = new InventoryLocation("Demo Closed Shop", "DEMO-CLOSED");
        inactive.Deactivate();
        if (!await db.Locations.AnyAsync(x => x.Code == "DEMO-CLOSED", ct)) db.Locations.Add(inactive);
        await db.SaveChangesAsync(ct);
        return shop.Id;
    }

    private static Guid HistoryId(string key) => new(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes($"{Marker}:{key}"))[..16]);

    private static async Task SeedSalesHistoryAsync(string connectionString, Guid[] shops,
        Guid cashierId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var sales = new SalesDbContext(new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(connection).Options);
        await using var stock = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(connection).Options);
        await using var transaction = await sales.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await stock.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);

        var priceList = await stock.PriceLists.SingleAsync(list => list.Name == "Retail" &&
            list.CurrencyCode == "DZD", ct);
        var variants = await stock.PriceListItems.AsNoTracking()
            .Where(item => item.PriceListId == priceList.Id && item.Price > 0)
            .OrderBy(item => item.ProductVariantId)
            .Select(item => new { item.ProductVariantId, item.Price }).Take(12).ToListAsync(ct);
        if (variants.Count == 0) return;
        var customerIds = await sales.Customers.Where(customer => customer.IsActive &&
            (customer.Email == "retail.demo@local.test" || customer.Email == "wholesale.demo@local.test"))
            .OrderBy(customer => customer.Email).Select(customer => customer.Id).ToListAsync(ct);
        var variantIds = variants.Select(item => item.ProductVariantId).ToArray();
        var balances = await stock.StockBalances.Where(balance =>
                shops.Contains(balance.LocationId) && variantIds.Contains(balance.VariantId))
            .ToDictionaryAsync(balance => (balance.LocationId, balance.VariantId), ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dates = Enumerable.Range(0, 84).Select(offset => today.AddDays(-offset)).Reverse();
        var fixtures = (from day in dates
            from shop in shops
            from slot in Enumerable.Range(0, 2)
            let id = HistoryId($"history:{shop:N}:{day:yyyy-MM-dd}:{slot}")
            select (day, shop, slot, id)).ToArray();
        var paymentIds = fixtures.Select(fixture => fixture.id).ToArray();
        var existing = (await sales.Payments.Where(payment => paymentIds.Contains(payment.Id))
            .Select(payment => payment.Id).ToListAsync(ct)).ToHashSet();
        foreach (var (day, shop, slot, id) in fixtures)
        {
            if (existing.Contains(id)) continue;
            var ordinal = day.DayNumber + slot + Array.IndexOf(shops, shop);
            var item = variants[ordinal % variants.Count];
            var quantity = 1 + ordinal % 4;
            var timestamp = new DateTimeOffset(day.ToDateTime(new TimeOnly(10 + slot * 5, ordinal % 60)), TimeSpan.Zero);
            Guid? customer = ordinal % 3 == 0 || customerIds.Count == 0 ? null : customerIds[ordinal % customerIds.Count];
            var sale = new Sale(shop, customer, cashierId, priceList.Id);
            var saleLine = sale.AddLine(item.ProductVariantId, quantity, item.Price, decimal.Round(item.Price * .55m, 2));
            if (ordinal % 4 == 0) sale.ApplySaleDiscount(SaleDiscountType.Percentage, 10m);
            sale.Complete(cashierId, timestamp);
            sale.RecordPayment(sale.Total);
            sales.Sales.Add(sale);
            sales.Payments.Add(new Payment(id, sale.Id, sale.Total,
                ordinal % 2 == 0 ? PaymentMethod.Cash : PaymentMethod.Card, cashierId, paidAt: timestamp));
            if (ordinal % 11 == 0 && day < today)
            {
                var returnQuantity = ordinal % 22 == 0 ? quantity : 1;
                var unitRefund = decimal.Round(sale.Total / quantity, 4, MidpointRounding.ToZero);
                var refund = new SaleReturn(HistoryId($"refund:{id:N}"), sale.Id, shop,
                    "Historical demo return: item not resellable", cashierId,
                    [new SaleReturnLineItem(saleLine.Id, item.ProductVariantId, returnQuantity, unitRefund, false)]);
                sale.RecordReturn(saleLine.Id, returnQuantity);
                sales.SaleReturns.Add(refund);
                sales.Entry(refund).Property(x => x.ReturnDate).CurrentValue = timestamp.AddDays(1);
            }

            if (!balances.TryGetValue((shop, item.ProductVariantId), out var balance))
            {
                balance = new StockBalance(item.ProductVariantId, shop);
                stock.StockBalances.Add(balance);
                balances.Add((shop, item.ProductVariantId), balance);
                var opening = new StockMovement(item.ProductVariantId, shop, StockMovementType.OpeningBalance,
                    0, Actor, reason: "Opening balance for demo history");
                stock.StockMovements.Add(opening);
                stock.Entry(opening).Property(x => x.OccurredAt).CurrentValue = timestamp.AddHours(-2);
            }
            // Add dedicated demo supply for each historical sale, preserving current operational balances.
            var supply = new StockMovement(item.ProductVariantId, shop, StockMovementType.Receipt,
                quantity, Actor, reference: sale.SaleNumber, reason: "Historical demo supply",
                referenceType: "DemoHistorySupply", referenceId: sale.Id, sourceLineNumber: 1);
            stock.StockMovements.Add(supply);
            stock.Entry(supply).Property(x => x.OccurredAt).CurrentValue = timestamp.AddHours(-1);
            var movement = new StockMovement(item.ProductVariantId, shop, StockMovementType.Sale,
                -quantity, Actor, reference: sale.SaleNumber, referenceType: "Sale", referenceId: sale.Id, sourceLineNumber: 1);
            stock.StockMovements.Add(movement);
            stock.Entry(movement).Property(x => x.OccurredAt).CurrentValue = timestamp;
        }
        await sales.SaveChangesAsync(ct);
        await stock.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task SeedPromotionsAsync(SalesDbContext sales, InventoryDbContext inventory,
        Guid variantId, CancellationToken ct)
    {
        var variant = await inventory.ProductVariants.AsNoTracking().Where(x => x.Id == variantId)
            .Select(x => new { x.ProductId, x.Product.BrandId, x.Product.ProductCategoryId }).SingleAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var fixtures = new (string Name, PromotionTarget Target, Guid? Id, SaleDiscountType Discount,
            decimal Value, DateTimeOffset Starts, DateTimeOffset? Ends, bool Enabled)[]
        {
            ("Demo everyday product offer", PromotionTarget.Product, variant.ProductId, SaleDiscountType.Percentage, 5m, now.AddDays(-7), null, true),
            ("Demo overlapping variant offer", PromotionTarget.Variant, variantId, SaleDiscountType.FixedAmount, 180m, now.AddDays(-7), now.AddDays(14), true),
            ("Demo upcoming category offer", PromotionTarget.Category, variant.ProductCategoryId, SaleDiscountType.Percentage, 15m, now.AddDays(3), now.AddDays(12), true),
            ("Demo expired brand offer", PromotionTarget.Brand, variant.BrandId, SaleDiscountType.Percentage, 20m, now.AddDays(-40), now.AddDays(-10), true),
            ("Demo disabled variant offer", PromotionTarget.Variant, variantId, SaleDiscountType.Percentage, 90m, now.AddDays(-1), null, false),
        };
        var existing = (await sales.Promotions.Select(x => x.Name).ToListAsync(ct)).ToHashSet();
        foreach (var fixture in fixtures)
        {
            if (fixture.Id is not Guid targetId || existing.Contains(fixture.Name)) continue;
            var promotion = new SalePromotion();
            promotion.Configure(fixture.Name, fixture.Target, targetId, fixture.Discount,
                fixture.Value, fixture.Starts, fixture.Ends, fixture.Enabled);
            sales.Promotions.Add(promotion);
        }
        await sales.SaveChangesAsync(ct);
    }

    private static async Task SeedHistoricalExpensesAsync(AccountingDbContext db, Guid[] shops,
        Guid managerId, CancellationToken ct)
    {
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Name == "Demo Operations", ct);
        if (category is null)
        {
            category = new ExpenseCategory("Demo Operations");
            db.ExpenseCategories.Add(category);
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = (await db.Expenses.Where(expense => expense.Description != null && expense.Description.StartsWith(Marker + " History"))
            .Select(expense => expense.Description).ToListAsync(ct)).ToHashSet();
        foreach (var shop in shops)
        {
            for (var offset = 0; offset < 84; offset++)
            {
                var day = today.AddDays(-offset);
                if (day.DayNumber % 7 != 0) continue;
                var description = $"{Marker} History {shop:N} {day:yyyy-MM-dd}";
                if (existing.Contains(description)) continue;
                db.Expenses.Add(new Expense(category.Id, shop, 2500m + day.DayNumber % 5 * 350m,
                    day, description, managerId));
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAccountingAsync(AccountingDbContext db,
        Guid mainId, Guid managerId, CancellationToken ct)
    {
        var rent = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Name == "Demo Rent", ct);
        if (rent is null)
        {
            rent = new ExpenseCategory("Demo Rent");
            db.ExpenseCategories.Add(rent);
        }
        if (!await db.ExpenseCategories.AnyAsync(x => x.Name == "Demo Utilities", ct))
            db.ExpenseCategories.Add(new ExpenseCategory("Demo Utilities"));
        await db.SaveChangesAsync(ct);

        if (!await db.Expenses.AnyAsync(x => x.Description == Marker + " Shop rent", ct))
            db.Expenses.Add(new Expense(rent.Id, mainId, 25000m,
                DateOnly.FromDateTime(DateTime.UtcNow), Marker + " Shop rent", managerId));
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAuditAsync(AuditDbContext db, Guid managerId,
        Guid entityId, CancellationToken ct)
    {
        if (await db.Entries.AnyAsync(x => x.Action == "DemoDataSeeded", ct)) return;
        db.Entries.Add(new AuditEntry(managerId, "DemoDataSeeded", "DemoFixture",
            entityId, null, "{\"source\":\"database-migrator\"}", Marker));
        await db.SaveChangesAsync(ct);
    }
}
