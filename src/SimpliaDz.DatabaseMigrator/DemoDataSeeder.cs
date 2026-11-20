using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SimpliaDz.Accounting.Core;
using SimpliaDz.Accounting.Infrastructure.Data;
using SimpliaDz.Audit.Core;
using SimpliaDz.Audit.Infrastructure.Data;
using SimpliaDz.BuildingBlocks.Tenancy.Abstractions;
using SimpliaDz.Identity.Core.Entities;
using SimpliaDz.Identity.Infrastructure.Data;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Organization.Core.Entities;
using SimpliaDz.Organization.Infrastructure.Data;
using SimpliaDz.Purchasing.Core.Entities;
using SimpliaDz.Purchasing.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.SharedKernel.Authorization;
using OrganizationProfile = SimpliaDz.Organization.Core.Entities.Organization;

internal static class DemoDataSeeder
{
    private const string Marker = "[SIMPLIADZ-DEMO-SEED]";
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
            completedSaleId = await SeedSalesAsync(services.GetRequiredService<SalesDbContext>(),
                tenant.ConnectionString, mainId, Variant("TSHIRT-BLK-M"),
                users.CashierId, inventory, ct);

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
            PermissionNames.CashSessions.Manage);
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
            db.Organizations.Add(new OrganizationProfile("SimpliaDz Demo Shop",
                "SimpliaDz Demo Shop", "+213 555 010 100", "demo@local.test",
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
        if (!await db.CashRegisters.AnyAsync(x => x.LocationId == mainId &&
            x.Code == "DEMO-REG-2", ct))
            db.CashRegisters.Add(new CashRegister(mainId, "DEMO-REG-2", "Demo Second Counter"));
        await db.SaveChangesAsync(ct);

        var session = await db.CashSessions.SingleOrDefaultAsync(x =>
            x.CashRegisterId == register.Id && x.Status == CashSessionStatus.Open, ct);
        if (session is null && !await db.CashSessions.AnyAsync(x => x.CashRegisterId == register.Id, ct))
        {
            session = new CashSession(register.Id, cashierId, 5000m);
            db.CashSessions.Add(session);
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
