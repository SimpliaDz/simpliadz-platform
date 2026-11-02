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
        new(Locations.View,     "View Locations",     "Locations"),
        new(Locations.Create,   "Create Locations",   "Locations"),
        new(Locations.Edit,     "Edit Locations",     "Locations"),
        new(Organization.View,   "View Organization",   "Organization"),
        new(Organization.Manage, "Manage Organization", "Organization")
    ];

    public static IReadOnlyList<string> All { get; } = Catalog.Select(definition => definition.Name).ToArray();
}
