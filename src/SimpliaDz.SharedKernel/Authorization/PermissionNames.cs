namespace SimpliaDz.SharedKernel.Authorization;

public sealed record PermissionDefinition(string Name, string DisplayName, string Category);

public static class PermissionNames
{
    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Edit = "users.edit";
        public const string Delete = "users.delete";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Manage = "roles.manage";
    }

    public static class Permissions
    {
        public const string View = "permissions.view";
        public const string Manage = "permissions.manage";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Create = "products.create";
        public const string Edit = "products.edit";
    }

    public static class Locations
    {
        public const string View = "locations.view";
        public const string Create = "locations.create";
        public const string Edit = "locations.edit";
    }

    public static class Categories
    {
        public const string View = "categories.view";
        public const string Create = "categories.create";
        public const string Edit = "categories.edit";
    }

    public static class Brands
    {
        public const string View = "brands.view";
        public const string Create = "brands.create";
        public const string Edit = "brands.edit";
    }

    public static class Attributes
    {
        public const string View = "attributes.view";
        public const string Manage = "attributes.manage";
    }

    public static class Variants
    {
        public const string View = "variants.view";
        public const string Create = "variants.create";
        public const string Edit = "variants.edit";
    }

    public static class PriceLists
    {
        public const string View = "price-lists.view";
        public const string Manage = "price-lists.manage";
    }

    public static class Stock
    {
        public const string View = "stock.view";
        public const string Manage = "stock.manage";
    }

    public static class Suppliers
    {
        public const string View = "suppliers.view";
        public const string Manage = "suppliers.manage";
    }

    public static class PurchaseOrders
    {
        public const string View = "purchase-orders.view";
        public const string Manage = "purchase-orders.manage";
    }

    public static class GoodsReceipts
    {
        public const string View = "goods-receipts.view";
        public const string Manage = "goods-receipts.manage";
    }

    public static class Customers
    {
        public const string View = "customers.view";
        public const string Manage = "customers.manage";
    }

    public static class Sales
    {
        public const string View = "sales.view";
        public const string Manage = "sales.manage";
    }

    public static class SaleDiscounts
    {
        public const string Apply = "sales.discounts.apply";
    }

    public static class SalePayments
    {
        public const string Manage = "sales.payments.manage";
    }

    public static class SaleReturns
    {
        public const string Manage = "sales.returns.manage";
    }

    public static class ExpenseCategories
    {
        public const string View = "expense-categories.view";
        public const string Manage = "expense-categories.manage";
    }

    public static class Expenses
    {
        public const string View = "expenses.view";
        public const string Manage = "expenses.manage";
    }

    public static class Reports
    {
        public const string SalesSummary = "reports.sales-summary.view";
        public const string ProductPerformance = "reports.product-performance.view";
        public const string Inventory = "reports.inventory.view";
        public const string LowStock = "reports.low-stock.view";
    }

    public static class Organization
    {
        public const string View = "organization.view";
        public const string Manage = "organization.manage";
    }

    public static IReadOnlyList<PermissionDefinition> Catalog { get; } =
    [
        new(Users.View,         "View Users",         "Users"),
        new(Users.Create,       "Create Users",       "Users"),
        new(Users.Edit,         "Edit Users",         "Users"),
        new(Users.Delete,       "Delete Users",       "Users"),
        new(Roles.View,         "View Roles",         "Roles"),
        new(Roles.Manage,       "Manage Roles",       "Roles"),
        new(Permissions.View,   "View Permissions",   "Permissions"),
        new(Permissions.Manage, "Manage Permissions", "Permissions"),
        new(Products.View,      "View Products",      "Products"),
        new(Products.Create,    "Create Products",    "Products"),
        new(Products.Edit,      "Edit Products",      "Products"),
        new(Categories.View,    "View Categories",    "Categories"),
        new(Categories.Create,  "Create Categories",  "Categories"),
        new(Categories.Edit,    "Edit Categories",    "Categories"),
        new(Brands.View,        "View Brands",        "Brands"),
        new(Brands.Create,      "Create Brands",      "Brands"),
        new(Brands.Edit,        "Edit Brands",        "Brands"),
        new(Attributes.View,    "View Attributes",    "Attributes"),
        new(Attributes.Manage,  "Manage Attributes",  "Attributes"),
        new(Variants.View,      "View Variants",      "Variants"),
        new(Variants.Create,    "Create Variants",    "Variants"),
        new(Variants.Edit,      "Edit Variants",      "Variants"),
        new(PriceLists.View,    "View Price Lists",   "Price Lists"),
        new(PriceLists.Manage,  "Manage Price Lists", "Price Lists"),
        new(Stock.View,         "View Stock",        "Stock"),
        new(Stock.Manage,       "Manage Stock",      "Stock"),
        new(Suppliers.View,     "View Suppliers",    "Suppliers"),
        new(Suppliers.Manage,   "Manage Suppliers",  "Suppliers"),
        new(PurchaseOrders.View, "View Purchase Orders", "Purchase Orders"),
        new(PurchaseOrders.Manage, "Manage Purchase Orders", "Purchase Orders"),
        new(GoodsReceipts.View, "View Goods Receipts", "Goods Receipts"),
        new(GoodsReceipts.Manage, "Receive Goods", "Goods Receipts"),
        new(Customers.View, "View Customers", "Customers"),
        new(Customers.Manage, "Manage Customers", "Customers"),
        new(Sales.View, "View Sales", "Sales"),
        new(Sales.Manage, "Manage Sales", "Sales"),
        new(SaleDiscounts.Apply, "Apply Sale Discounts", "Sales"),
        new(SalePayments.Manage, "Record Sale Payments", "Sales"),
        new(SaleReturns.Manage, "Process Sale Returns", "Sales"),
        new(ExpenseCategories.View, "View Expense Categories", "Accounting"),
        new(ExpenseCategories.Manage, "Manage Expense Categories", "Accounting"),
        new(Expenses.View, "View Expenses", "Accounting"),
        new(Expenses.Manage, "Manage Expenses", "Accounting"),
        new(Reports.SalesSummary, "View Sales Summary Report", "Reporting"),
        new(Reports.ProductPerformance, "View Product Performance Report", "Reporting"),
        new(Reports.Inventory, "View Inventory Report", "Reporting"),
        new(Reports.LowStock, "View Low Stock Report", "Reporting"),
        new(Locations.View,     "View Locations",     "Locations"),
        new(Locations.Create,   "Create Locations",   "Locations"),
        new(Locations.Edit,     "Edit Locations",     "Locations"),
        new(Organization.View,   "View Organization",   "Organization"),
        new(Organization.Manage, "Manage Organization", "Organization")
    ];

    public static IReadOnlyList<string> All { get; } = Catalog.Select(definition => definition.Name).ToArray();
}
