# SimpliaDz — Technical Guide

> **Purpose of this document:** explain, in plain language, how this solution is put together — so you (a human still learning this stack) and future Claude Code sessions make changes that fit the existing architecture instead of introducing new patterns.
>
> This is a **snapshot of what the code actually does today** (verified by reading the source, not guessed). Where something looks unfinished or inconsistent, it's called out explicitly in [Section 12](#12-whats-unfinished-inconsistent-or-a-known-gap) rather than glossed over.
>
> This document does not change any production code. See also `CLAUDE.md` at the repo root for command references and a more compact spec — this guide is the "why and how it fits together" companion to it.

---

## Table of contents

1. [Big picture: what kind of app is this](#1-big-picture-what-kind-of-app-is-this)
2. [Solution structure — what's in each folder](#2-solution-structure--whats-in-each-folder)
3. [The modular monolith: how modules are organized](#3-the-modular-monolith-how-modules-are-organized)
4. [Clean Architecture inside a module](#4-clean-architecture-inside-a-module)
5. [Walkthrough #1: Identity module, "CreateUser"](#5-walkthrough-1-identity-module-createuser-command)
6. [Walkthrough #2: Inventory module, "CreateProduct"](#6-walkthrough-2-inventory-module-createproduct-command)
7. [Repository pattern vs. Service pattern — which to use when](#7-repository-pattern-vs-service-pattern--which-to-use-when)
8. [Mediator: how CQRS is wired up](#8-mediatr-how-cqrs-is-wired-up)
9. [FluentValidation: how validation is wired up](#9-fluentvalidation-how-validation-is-wired-up)
10. [Dependency Injection: who registers what, and when](#10-dependency-injection-who-registers-what-and-when)
11. [Multi-tenancy: how it's supposed to work vs. how it works today](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today)
12. [What's unfinished, inconsistent, or a known gap](#12-whats-unfinished-inconsistent-or-a-known-gap)
13. [Conventions to follow when adding code](#13-conventions-to-follow-when-adding-code)
14. [Implementation status recap](#14-implementation-status-recap)

---

## 1. Big picture: what kind of app is this

This is a **modular monolith**: a single deployable ASP.NET Core application (`SimpliaDz.Api`) that is internally split into independent **modules** (Identity, Inventory, Organization, and six not-yet-implemented ones), each structured using **Clean Architecture** layers, with **CQRS via Mediator** inside each module.

Why this combination, in plain terms:

- **Modular monolith** (as opposed to microservices): all the code runs in one process and one deployment, but it's organized as if it *could* be split into services later. Each module owns its own database schema and never reaches into another module's tables directly. You get most of the maintainability benefits of microservices (isolation, clear boundaries) without the operational cost (network calls, distributed transactions, separate deployments) — a reasonable choice for a product that isn't yet at a scale that needs real microservices.
- **Clean Architecture** inside each module: the code is arranged in layers where the "inner" layer (business rules) never depends on the "outer" layer (database, web framework). This means you can change the database or the web framework without rewriting business logic, and you can unit-test business logic without spinning up a real database.
- **CQRS with Mediator**: instead of big "service" classes with a dozen methods, each individual use case (e.g. "create a user", "get a role by id") is its own small class (a `Command` or `Query` + its `Handler`). This keeps each use case easy to find, read, and test in isolation, and gives you one consistent place (a Mediator pipeline) to bolt on cross-cutting behavior like validation.

If you remember one thing from this section: **every module repeats the same four-layer shape, and every use case inside a module repeats the same three-file shape (request / handler / validator).** Once you've understood one module and one use case, you've understood the whole codebase's shape.

---

## 2. Solution structure — what's in each folder

```
src/
├── SimpliaDz.Api/                 # The ONE web application that gets deployed/run.
├── SimpliaDz.BuildingBlocks/       # Shared plumbing code used by every module (not business logic).
├── SimpliaDz.SharedKernel/         # Reserved for shared domain concepts — currently EMPTY (no code yet).
├── SimpliaDz.ModuleCatalog/         # Shared installed-module list used by both executable hosts.
├── SimpliaDz.DatabaseMigrator/     # A separate CLI tool that runs migrations + seed data per tenant.
└── Modules/
    ├── Identity/                   # ACTIVE — auth, users, roles, permissions.
    ├── Inventory/                  # ACTIVE — product catalog, variants, locations, stock.
    ├── Purchasing/                # ACTIVE — suppliers, purchase orders, goods receipts.
    ├── Sales/                     # ACTIVE — customer management.
    └── Audit | Accounting | Notifications | Reporting/   # SCAFFOLDED, NOT IMPLEMENTED.
tests/                              # Module-catalog, Organization, Inventory, Purchasing, and Sales tests.
```

### `SimpliaDz.Api` — the host

This is the actual web server. It doesn't contain business logic — its job is to **wire everything together** and expose HTTP endpoints.

- `Program.cs` — the entry point. It's intentionally tiny:
  ```csharp
  builder.Services.AddApplicationServices(builder.Configuration);
  builder.Services.AddJwtAuthentication(builder.Configuration);
  var app = builder.Build();
  app.UseAppMiddleware();
  app.MapAppEndpoints();
  app.Run();
  ```
- `Modules/ModuleRegistry.cs` — a thin API wrapper over `SimpliaDz.ModuleCatalog`; the API registers all installed infrastructure at startup, then tenant middleware enables or rejects module endpoints per request (see [Section 3](#3-the-modular-monolith-how-modules-are-organized)).
- `Extensions/DependencyInjectionExtensions.cs` — registers every module's services, Mediator, FluentValidation, and authorization policies.
- `Extensions/AuthenticationExtensions.cs` — configures JWT Bearer authentication.
- `Extensions/MiddlewareExtensions.cs` — configures the global exception handler, Swagger, and the auth middleware pipeline.
- `Extensions/EndpointExtensions.cs` — maps every module's HTTP endpoint groups onto the app.
- `Endpoints/Identity/*`, `Endpoints/Inventory/*`, `Endpoints/Organization/*` — the actual Minimal API route definitions (thin — they just call Mediator and return a result, see the walkthroughs below).
- `Configuration/JwtOptions.cs` — strongly-typed JWT settings bound from `appsettings.json`.

### `SimpliaDz.BuildingBlocks` — shared cross-cutting plumbing

Code every module can depend on, but which is **not** business logic:

- `Exceptions/NotFoundException.cs` — a generic "entity not found" exception any module can throw; the API's global exception handler knows how to turn it into an HTTP 404.
- `Mediation/ValidationBehavior.cs` — the Mediator pipeline step that runs FluentValidation before every handler (details in [Section 9](#9-fluentvalidation-how-validation-is-wired-up)).
- `Modules/Abstractions/IModuleInstaller.cs` — the contract every module implements to plug itself into the host (details in [Section 3](#3-the-modular-monolith-how-modules-are-organized)).
- `Tenancy/` — `ITenantContext`, `ITenantResolver`, `ITenantStore`, `TenancyOptions`, `ConfigurationTenantStore`, `TenantContext`, `CliTenantResolver` — the multi-tenancy abstractions (details in [Section 11](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today)).

### `SimpliaDz.SharedKernel`

Exists as an empty class-library project (just the `.csproj`, no `.cs` files). It's a placeholder for domain primitives that might eventually be shared *between modules* (e.g. a `Money` value object used by both Sales and Accounting). Every module already has a `ProjectReference` to it, so when something genuinely shared shows up, this is where it goes — don't put it inside one module's `Core` and reference it from another module.

### `SimpliaDz.ModuleCatalog`

A small composition project, not a business module. It references every module's Infrastructure project and exposes the authoritative `ModuleCatalog.GetModules()` list of installed `IModuleInstaller` implementations. Both the API and migrator reference it, which prevents them from maintaining separate installer lists. Tenant configuration still decides which entries from that list each host activates. See [Module Catalog](MODULE_CATALOG.md) for its dependency rationale and maintenance steps.

### `SimpliaDz.DatabaseMigrator`

A separate console app (not the web API) whose only job is: for a given `--tenant <id>`, figure out which modules are enabled for that tenant, and run `MigrateAsync` + `SeedAsync` for each of them. This is the tool you actually run to create/update a tenant's database. See [Section 3](#3-the-modular-monolith-how-modules-are-organized) and [Section 11](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today) for exactly how it decides what to run.

### `Modules/<ModuleName>/`

Each module is four separate class-library projects — see [Section 4](#4-clean-architecture-inside-a-module) for what goes in each and why. Five modules (**Identity**, **Inventory**, **Organization**, **Purchasing**, **Sales**) have real code. Four (**Audit, Accounting, Notifications, Reporting**) remain scaffolds — see [Section 12](#12-whats-unfinished-inconsistent-or-a-known-gap). See [Customers](CUSTOMERS.md), [Suppliers](SUPPLIERS.md), and the feature-specific module guides for contracts and rules.

### `tests/`

Contains module-level test projects, including Purchasing and Sales tests.

---

## 3. The modular monolith: how modules are organized

**Isolation rule:** each module owns its own `DbContext`, its own database schema (e.g. `identity`, `inventory`), and its own EF Core migrations. Verified check: no module's code references another module's `DbContext`, entities, or project — every module's `.csproj` only has `ProjectReference`s to its own sibling projects (`Core`/`UseCases`/`Infrastructure`/`Contracts`) plus the two shared projects `SharedKernel` and `BuildingBlocks`. There are zero cross-module `using` statements anywhere in `src/Modules/**`.

**How a module plugs into the host — `IModuleInstaller`:**

```csharp
// src/SimpliaDz.BuildingBlocks/Modules/Abstractions/IModuleInstaller.cs
public interface IModuleInstaller
{
    string ModuleName { get; }
    void Register(IServiceCollection services, string connectionString);
    Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
    Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
```

Every module has exactly one class implementing this (e.g. `IdentityModuleInstaller`, `InventoryModuleInstaller`) living in its `Infrastructure` project. Think of it as the module's "plug": `Register` wires its `DbContext` and services into DI, `MigrateAsync` applies its EF Core migrations, `SeedAsync` inserts its seed data.

**Where modules get listed — one shared catalog:**

`SimpliaDz.ModuleCatalog/ModuleCatalog.cs` is the one place that constructs all nine installed installers (Identity, Inventory, Organization, Sales, Purchasing, Accounting, Notifications, Reporting, Audit). The API's `ModuleRegistry` delegates to this catalog, and the migrator registers its `IModuleInstaller` instances from the same catalog.

Each host then applies its tenant configuration:

- The API registers all installed Infrastructure modules once at startup. Its local configuration selects one active tenant for the API instance. Tenant-scoped requests use that context's connection string for EF Core and return 404 for an endpoint belonging to a disabled module; system endpoints do not resolve tenant state.
- The migrator resolves the selected `--tenant`, then registers, migrates, and seeds only modules for which `tenant.IsModuleEnabled(installer.ModuleName)` is true.

This removes installer-list drift. It does **not** make scaffolded modules functional: a module still needs handlers, endpoint mappings, and its UseCases assembly included in the API scans before it exposes behavior.

**How to add a new module** (from `CLAUDE.md`, confirmed against the code):

1. Create the four projects: `SimpliaDz.<Module>.Core`, `.UseCases`, `.Infrastructure`, `.Contracts`.
2. In `Infrastructure`, implement `IModuleInstaller`.
3. Add the Infrastructure project reference and installer to `SimpliaDz.ModuleCatalog/ModuleCatalog.cs`.
4. Enable the module for the relevant tenant(s) in the shared local `src/tenants.json` catalog under `Tenancy:Tenants:<tenant>:Modules`.
5. Update `tests/SimpliaDz.ModuleCatalog.Tests` with the expected module name.
6. Add its `UseCases` assembly to the Mediator/FluentValidation assembly-scan list in `DependencyInjectionExtensions.cs`.
7. Add its endpoint-mapping call in `EndpointExtensions.cs`.

---

## 4. Clean Architecture inside a module

Every module (active or stub) is split into four projects, and the **dependency arrows only point inward**:

```
   Api  ─────────────────────────────────────────────►  (calls into UseCases via Mediator)
    │
    ▼
Infrastructure ──► UseCases ──► Contracts
    │                 │
    └────────────────►Core◄────────────────────────────  (Core depends on nothing)
```

| Layer | What lives here | What it may depend on |
|---|---|---|
| **`<Module>.Core`** | Domain entities, value objects, domain exceptions. The actual business rules. | Nothing (no NuGet packages). Framework types are allowed only when the domain itself needs them — e.g. Identity's entities extend ASP.NET Core Identity's `IdentityUser<Guid>`. |
| **`<Module>.UseCases`** | Mediator `Command`/`Query` + `Handler` + FluentValidation `Validator`, and the **interfaces** (repository or service abstractions) that handlers depend on. | Mediator, FluentValidation, `BuildingBlocks`, its own `Core`. **Never** EF Core — this layer must not know the database exists. |
| **`<Module>.Infrastructure`** | EF Core `DbContext`, entity configurations, migrations, the concrete classes that implement the `UseCases` interfaces, seeders, the `IModuleInstaller`. | EF Core, SQL Server, ASP.NET Core, its own `Core` + `UseCases`. |
| **`<Module>.Contracts`** | Plain DTOs (request/response records) shared between the API layer and `UseCases`. | Nothing. |

**Why this shape, in plain terms:** `UseCases` (where your business workflows live) only ever sees *interfaces* like `IProductRepository` or `IIdentityUserService` — never `DbContext`, never `SqlConnection`. This is "dependency inversion": the abstraction lives with the thing that needs it (`UseCases`), and the concrete implementation lives in the layer that knows about the database (`Infrastructure`), which then gets wired together only at startup via DI. Concretely, this means you could swap SQL Server for PostgreSQL, or ASP.NET Identity for a different auth provider, by rewriting only the `Infrastructure` project — the `Core` and `UseCases` code (your actual business rules and workflows) wouldn't need to change at all.

The two walkthroughs below trace one real use case through all four layers each, for both of the two "shapes" this repo uses (service pattern and repository pattern).

---

## 5. Walkthrough #1: Identity module, "CreateUser" command

This traces a `POST /users` request end-to-end.

**1. Contracts** — plain DTOs, no logic (`src/Modules/Identity/SimpliaDz.Identity.Contracts/Users/CreateUserRequest.cs`):
```csharp
public sealed record CreateUserRequest(
    string Email, string FirstName, string LastName, string Password, IList<string>? Roles = null);
```

**2. API endpoint** (`src/SimpliaDz.Api/Endpoints/Identity/UserEndpoints.cs`) — binds the request, sends it through Mediator, returns the result. No business logic here:
```csharp
group.MapPost("/", async ([FromBody] CreateUserRequest request, [FromServices] ISender sender, CancellationToken ct) =>
{
    var id = await sender.Send(
        new CreateUserCommand(request.Email, request.FirstName, request.LastName, request.Password, request.Roles), ct);
    return Results.Created($"/users/{id}", new { id });
}).RequireAuthorization(PermissionNames.Users.Create);
```
`PermissionNames.Users.Create` resolves to `users.create`; that value is both the policy name and the exact JWT claim value it checks for.

**3. UseCases** — the command, handler, and validator (`src/Modules/Identity/SimpliaDz.Identity.UseCases/Users/Commands/CreateUser/`):
```csharp
public sealed record CreateUserCommand(
    string Email, string FirstName, string LastName, string Password, IList<string>? Roles) : IRequest<Guid>;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly IIdentityUserService _userService;
    public CreateUserHandler(IIdentityUserService userService) => _userService = userService;

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken ct) =>
        await _userService.CreateUserAsync(request.Email, request.FirstName, request.LastName, request.Password, request.Roles, ct);
}

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
```
Notice the handler depends only on `IIdentityUserService` — an interface. It has no idea EF Core or ASP.NET Identity exist.

**4. Infrastructure** — the concrete implementation (`src/Modules/Identity/SimpliaDz.Identity.Infrastructure/Users/IdentityUserService.cs`), which is where ASP.NET Core Identity's `UserManager<AppUser>` actually gets used:
```csharp
internal sealed class IdentityUserService : IIdentityUserService
{
    public async Task<Guid> CreateUserAsync(string email, string firstName, string lastName, string password,
        IList<string>? roles, CancellationToken ct)
    {
        var user = new AppUser { UserName = email, Email = email, FirstName = firstName, LastName = lastName,
            EmailConfirmed = true, IsActive = true };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(/* ... */);
        if (roles is { Count: > 0 }) await _userManager.AddToRolesAsync(user, roles);
        return user.Id;
    }
}
```
Note it's declared `internal` — code outside this project literally cannot construct it directly; the only way to get one is through the `IIdentityUserService` interface via DI. That's the dependency-inversion boundary enforced by the compiler, not just convention.

**5. Core entities** (`src/Modules/Identity/SimpliaDz.Identity.Core/Entities/`):
```csharp
public sealed class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
```
`AppUser`/`AppRole` extend ASP.NET Core Identity's own base classes (with a `Guid` key instead of the default `string`). `Permission` and `RolePermission` are custom entities layered on top to support fine-grained, permission-string-based authorization (see below).

**6. Database** (`src/Modules/Identity/SimpliaDz.Identity.Infrastructure/Data/IdentityDbContext.cs`):
```csharp
public sealed class IdentityDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("identity");
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
```
Schema `identity`. Two migrations exist: `Identity_Initial` (base ASP.NET Identity tables) and `Identity_PermissionsAndProfile` (adds `Permissions`/`RolePermissions` tables and the `FirstName`/`LastName`/`IsActive` profile fields).

**Permission-based authorization, end to end:**
1. Permission names are constants in `SharedKernel/Authorization/PermissionNames.cs`. `IdentitySeeder` seeds their display metadata and grants all permissions to the `Admin` role. The catalog includes users, roles, permissions, products, product categories, business locations, and organization permissions.
2. On login, `JwtTokenGenerator` puts one JWT claim per effective permission into the token:
   ```csharp
   foreach (var permission in permissions)
       claims.Add(new Claim("permission", permission));
   ```
3. At startup, `DependencyInjectionExtensions.cs` turns every permission string into its own ASP.NET Core authorization **policy**, named after the permission itself:
   ```csharp
   foreach (var permission in PermissionNames.All)
       options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission));
   ```
4. Endpoints call `.RequireAuthorization(PermissionNames.Users.Create)` — the constant value is both the policy name and the exact claim value it checks for.

Dev-seeded admin login: `admin@dev.local` / `Admin123!` (see [Section 12](#12-whats-unfinished-inconsistent-or-a-known-gap) for a note on this).

---

## 6. Walkthrough #2: Inventory product catalog

Same shape, different flavor of abstraction (repository instead of service — see [Section 7](#7-repository-pattern-vs-service-pattern--which-to-use-when)).

**Core entity + value object** (`src/Modules/Inventory/SimpliaDz.Inventory.Core/`):
```csharp
// ValueObjects/ProductName.cs
public readonly record struct ProductName
{
    public const int MaxLength = 200;
    public string Value { get; }
    public ProductName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidProductNameException("Product name is required.");
        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength) throw new InvalidProductNameException($"Product name cannot exceed {MaxLength} characters.");
        Value = trimmed;
    }
}
```
A **value object**: it has no identity of its own, it's defined entirely by its value, and it makes "an invalid product name" impossible to construct — the validation lives in the constructor, not scattered across every place that sets a name. `Sku` follows the identical pattern.

`Product` is the **aggregate root** — the one entity other code is allowed to load/save as a unit; it owns its `ProductVariant` children and enforces invariants like "no two variants on the same product can share a SKU":
```csharp
public void AddVariant(string sku, decimal retailPrice, decimal wholesalePrice, string? barcode, string? color, string? size)
{
    var newSku = new Sku(sku);
    if (_variants.Any(v => v.Sku.Value.Equals(newSku.Value, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"Variant SKU '{newSku.Value}' already exists for this product.");
    // ...
}
```

**UseCases** — the repository abstraction (`src/Modules/Inventory/SimpliaDz.Inventory.UseCases/Products/Abstractions/IProductRepository.cs`):
```csharp
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Product>> SearchAsync(string? search, bool? isActive, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct);
    Task<bool> IsValidBrandAsync(Guid brandId, CancellationToken ct);
    Task<bool> IsValidCategoryAsync(Guid categoryId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```
The create/update handlers depend only on this interface. They enforce code uniqueness, verify that optional brand/category references point to active records, mutate the aggregate, and save the unit of work. There is deliberately no repository delete operation: products are deactivated so transaction and stock history remain valid.

**Infrastructure** — the EF Core implementation (`src/Modules/Inventory/SimpliaDz.Inventory.Infrastructure/Data/Repositories/EfProductRepository.cs`):
```csharp
internal sealed class EfProductRepository : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct);
    // Search/reference checks/AddAsync/SaveChangesAsync are implemented with the same DbContext.
}
```

**Value objects mapped to plain columns** (`src/Modules/Inventory/SimpliaDz.Inventory.Infrastructure/Data/Config/Catalog/ProductConfiguration.cs`):
```csharp
b.Property(x => x.Name)
    .HasConversion(v => v.Value, v => new ProductName(v))
    .HasMaxLength(ProductName.MaxLength)
    .IsRequired();
```
This is the trick that lets `Product.Name` be a rich `ProductName` value object in C# while the database column stays a plain `nvarchar` — EF converts in both directions, and because it round-trips through the value object's own constructor, validation re-runs even when data is loaded back out of the database.

**API endpoints** (`src/SimpliaDz.Api/Endpoints/Inventory/ProductEndpoints.cs`): `GET /products` supports `search` and `isActive` filters; `GET /products/{id}`, `POST /products`, `PUT /products/{id}`, and `PUT /products/{id}/deactivate` complete the catalog workflow. Read/create/edit operations require the matching `PermissionNames.Products` permission.

`Product` contains commercial information and owns `ProductVariant` children, but contains no stock quantity. A variant is the sellable/stockable item. Quantity is stored separately by `StockItem` using `(VariantId, LocationId)`, so the same SKU can hold different quantities in different stores or warehouses. Product hard deletion is not exposed, and the database uses restrictive foreign keys so variants and their transaction/stock history cannot be removed through a cascading product delete. See [Product Catalog](PRODUCT_CATALOG.md) for the complete contract and rules.

Business locations extend the same Inventory module rather than introducing another module. `GET/POST/PUT /locations` and `PUT /locations/{id}/deactivate` expose the `InventoryLocation` aggregate. Codes are normalized and unique inside each tenant database, deactivation retains stock history, and `StockService` rejects inactive or unknown locations before receiving or adjusting stock. See `BUSINESS_LOCATIONS.md` for the complete contract.

Product categories also stay inside Inventory. `ProductCategory` has an optional self-reference, sibling-level name uniqueness, restrictive deletion, and application-level ancestry checks that prevent circular relationships. `GET /categories` returns the hierarchy recursively; create, update, retrieve, and deactivate operations use the `categories.view/create/edit` permissions. Deactivation never changes existing product references. See [Product Categories](PRODUCT_CATEGORIES.md).

Brand management uses the same Inventory layers. `GET/POST/PUT /brands` and `PUT /brands/{id}/deactivate` expose the existing `Brand` entity through `brands.view/create/edit` permissions. Its unique name index applies to active and inactive records; deactivation preserves product references. See [Brands](BRANDS.md).

Generic product attributes live in Inventory as `ProductAttribute` and `AttributeValue`. `GET/POST /attributes` and nested value creation/order endpoints expose Size, Color, and future attribute types through `attributes.view/manage` permissions. The existing variant text fields remain compatible with stock data. See [Product Attributes](PRODUCT_ATTRIBUTES.md).

New product variants link to multiple attribute values through `VariantAttributeValues`. A unique product and combination key prevents duplicate selections regardless of value order. SKU and barcode lookup and deactivation are exposed through `/variants`; stock continues to reference variant IDs. See [Product Variants](PRODUCT_VARIANTS.md).

Retail and wholesale prices can be managed in tenant price lists. The price list migration copies existing variant prices into default DZD lists, and the API resolves a variant's price through a list. See [Price Lists](PRICE_LISTS.md).

---

## 7. Repository pattern vs. Service pattern — which to use when

You'll notice **Identity uses "services"** (`IIdentityUserService`, `IIdentityAuthService`, `IIdentityRoleService`, `IPermissionService`) while **Inventory uses a "repository"** (`IProductRepository`). Both are valid implementations of the *same* rule (`UseCases` defines the interface, `Infrastructure` implements it against EF Core) — the difference is just the shape of the abstraction, chosen per module:

- **Repository pattern** (Inventory): one interface per aggregate root, exposing only the persistence operations its use cases need (`GetByIdAsync`, `SearchAsync`, `AddAsync`, `SaveChangesAsync`). Reach for this when the module is plain EF Core domain persistence — you're the one designing the whole data-access surface.
- **Service pattern** (Identity): one interface per capability, exposing workflow-shaped verbs (`CreateUserAsync`, `AssignRolesAsync`, `GetEffectivePermissionsAsync`). Reach for this when you're wrapping an existing framework API that already has its own manager/store abstraction (here, ASP.NET Core Identity's `UserManager<T>`/`RoleManager<T>`) — a generic repository would just be an awkward re-wrap of something that already has a good API.

**Rule of thumb for new modules:** if you're persisting your own domain aggregate with plain EF Core, use a repository named after the aggregate (`IOrderRepository`, `IInvoiceRepository`). If you're wrapping a third-party subsystem that already exposes its own manager/store API, wrap it in a service interface instead of forcing a repository shape onto it.

---

## 8. Mediator: how CQRS is wired up

**Registration** (`src/SimpliaDz.Api/Extensions/DependencyInjectionExtensions.cs`):
```csharp
services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies =
    [
        typeof(SimpliaDz.Inventory.UseCases.AssemblyMarker),
        typeof(SimpliaDz.Identity.UseCases.AssemblyMarker),
        typeof(SimpliaDz.Organization.UseCases.AssemblyMarker)
    ];
});
```
The MIT-licensed `Mediator.SourceGenerator` scans these referenced assemblies at compile time and generates the dispatcher and handler registrations. The active Identity, Inventory, and Organization `UseCases` assemblies are included — a new module's handlers won't be found until its marker type is added to this array (see [Section 3](#3-the-modular-monolith-how-modules-are-organized), step 6). UseCases and BuildingBlocks reference only `Mediator.Abstractions`; the source generator exists only in the API composition root.

**The pattern per use case**, one folder per use case:
```
UseCases/<Domain>/<Commands|Queries>/<UseCaseName>/
    <Name>Command.cs      # or <Name>Query.cs — the IRequest<TResponse>
    <Name>Handler.cs       # IRequestHandler<TRequest, TResponse>
    <Name>Validator.cs      # optional AbstractValidator<TRequest>
```
Example: `UseCases/Users/Commands/CreateUser/{CreateUserCommand, CreateUserHandler, CreateUserValidator}.cs`.

**Why "vertical slices" instead of one big `UserService` class:** everything about one use case (its input, its logic, its rules) lives in one small folder, instead of being one method buried inside a 500-line service class alongside 20 unrelated methods. To understand or change "create a user", you open exactly one folder.

**The endpoint just sends a request and returns the response** — it never talks to a repository or service directly:
```csharp
var id = await sender.Send(new CreateUserCommand(...), ct);
```

**Pipeline behaviors** run around every handler. Right now there's exactly one:
```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```
`ValidationBehavior<TRequest,TResponse>` (`src/SimpliaDz.BuildingBlocks/Mediation/ValidationBehavior.cs`) wraps every `Handle` call: it runs all registered validators for `TRequest` *before* calling the actual handler, and short-circuits with a `FluentValidation.ValidationException` if any fail. If you need cross-cutting behavior like logging or transactions later, this is the place to add another `IPipelineBehavior<,>` — no handler code needs to change.

---

## 9. FluentValidation: how validation is wired up

**Registration** — the same active use-case assemblies as Mediator:
```csharp
services.AddValidatorsFromAssemblies(useCaseAssemblies);
```
This finds every `AbstractValidator<T>` and registers it as `IValidator<T>`.

**How it actually runs** (`src/SimpliaDz.BuildingBlocks/Mediation/ValidationBehavior.cs`):
```csharp
public async ValueTask<TResponse> Handle(
    TRequest request,
    MessageHandlerDelegate<TRequest, TResponse> next,
    CancellationToken ct)
{
    if (_validators.Any())
    {
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);
    }
    return await next(request, ct);
}
```
All validators for a request run concurrently, their failures get merged into one exception. That exception type is caught centrally:

```csharp
// src/SimpliaDz.Api/Extensions/MiddlewareExtensions.cs — inside the global exception handler
ValidationException            -> 400, body: { Errors: [{ PropertyName, ErrorMessage }] }
NotFoundException               -> 404
UnauthorizedAccessException     -> 401
InvalidOperationException       -> 400
(anything else)                  -> 500, generic { Error: "ServerError" }
```
**The upshot:** a handler or validator never has to know or care about HTTP status codes — it just throws the right *type* of exception (or FluentValidation does it for you), and one central piece of middleware turns that into the right response. If you add a new domain exception type, remember it needs to be added to this exception-handler mapping, or it'll fall through to a generic 500.

**Convention:** a validator is optional per use case (some simple queries have none), but when a command takes user input, add one. Keep rules declarative (`RuleFor(...)`) — don't put `if` logic that belongs in the domain (e.g. "does this SKU already exist on this product") into a validator; that kind of rule belongs in the handler/domain instead, since it needs to check the database or enforce an aggregate invariant.

---

## 10. Dependency Injection: who registers what, and when

Everything is registered from `Program.cs`, in this order:

```csharp
builder.Services.AddApplicationServices(builder.Configuration);   // 1
builder.Services.AddJwtAuthentication(builder.Configuration);      // 2
var app = builder.Build();
app.UseAppMiddleware();                                             // 3
app.MapAppEndpoints();                                              // 4
```

**Step 1 — `AddApplicationServices`** (`src/SimpliaDz.Api/Extensions/DependencyInjectionExtensions.cs`), in order:
1. Swagger / `EndpointsApiExplorer`.
2. Read the shared tenant catalog and validate the active tenant selected for this API instance. See [Section 11](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today).
3. Register the configuration-backed tenant store, scoped tenant context, and `ConfigurationTenantResolver`.
4. Get all installed modules from `ModuleRegistry`/`ModuleCatalog` and call `installer.Register(...)`. Each module's `DbContext` resolves `ITenantContext.ConnectionString` when the context is created for a request, rather than capturing a connection string at startup.
5. `AddMediator` + `AddValidatorsFromAssemblies` over the active modules' `UseCases` assemblies.
6. `AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))`.
7. `AddAuthorization(...)` — builds one policy per permission string.

**Step 2 — `AddJwtAuthentication`** (`src/SimpliaDz.Api/Extensions/AuthenticationExtensions.cs`): binds+validates `JwtOptions` (fails fast at startup if `SigningKey` is missing or under 32 characters), then `AddAuthentication().AddJwtBearer(...)`.

**Lifetimes used throughout:**
| Service kind | Lifetime | Why |
|---|---|---|
| `DbContext` (each module's) | Scoped (EF Core default via `AddDbContext`) | One instance per HTTP request / unit of work. |
| Repositories / application services (`EfProductRepository`, `IdentityUserService`, ...) | Scoped | They hold a scoped `DbContext`, so they must match its lifetime. |
| Mediator and handlers | Scoped (explicitly configured) | Handlers consume request-scoped repositories and `DbContext` instances. |
| `IValidator<T>` | Transient (FluentValidation default) | Same reasoning. |
| `IPipelineBehavior<,>` (`ValidationBehavior`) | Transient | Registered once as an open generic, applies to every request type. |
| `ITenantStore` | Singleton | Config-backed, safe to share app-wide. |
| `ITenantContext` | Scoped | Resolved once per scope from `ITenantResolver` + `ITenantStore`. |

**Rule for new code:** register your module's own services **inside your module's `IModuleInstaller.Register(...)`**, not in `DependencyInjectionExtensions.cs`. That file should stay generic (loop over modules, wire Mediator/FluentValidation/authorization) and never accumulate module-specific `AddScoped` calls directly.

---

## 11. Multi-tenancy: how it's supposed to work vs. how it works today

**The abstractions** (`src/SimpliaDz.BuildingBlocks/Tenancy/`):
```csharp
public interface ITenantResolver { string ResolveTenantId(); }
public interface ITenantStore    { TenantDescriptor GetTenant(string tenantId); }
public interface ITenantContext  { string TenantId; string Name; string ConnectionString; bool IsModuleEnabled(string moduleName); }
```
Local config shape (`src/tenants.json`):
```json
"Tenancy": {
  "ActiveTenantId": "dev",
  "Tenants": {
    "dev": {
      "Name": "Development",
      "ConnectionString": "Server=localhost,1433;Database=SimpliaDz_Dev;...",
      "Modules": { "Identity": true, "Inventory": true, "Sales": false, "...": false }
    }
  }
}
```

**How the CLI migrator uses this (the way it's *meant* to work):**
```
--tenant dev  →  CliTenantResolver.ResolveTenantId()  →  ConfigurationTenantStore.GetTenant("dev")
             →  TenantContext (wraps the TenantDescriptor: connection string + per-module flags)
             →  for each of the 8 registered IModuleInstaller:
                     if tenant.IsModuleEnabled(installer.ModuleName) → Register → MigrateAsync → SeedAsync
                     else → skip, log "module disabled for tenant"
```
This is genuinely tenant-aware: a different tenant with a different `Modules` map gets a different set of tables/migrations run against its own connection string.

**How the running web API works now:**

1. `ConfigurationTenantResolver` reads `Tenancy:ActiveTenantId`. The API validates that descriptor during startup, so a bad ID, name, or connection string fails before requests are accepted.
2. Clients do not provide a tenant header and cannot choose another configured database. One API process represents one active tenant environment.
3. Tenant endpoint groups carry both `TenantRequiredMetadata` and `ModuleEndpointMetadata`. Middleware resolves `ITenantContext` only for those endpoints and returns 404 when their module is disabled.
4. Each module's `AddDbContext` registration obtains `ITenantContext.ConnectionString` when EF Core creates the context.
5. Root, health, and Swagger endpoints are system-scoped and do not carry tenant metadata.
6. Login issues a JWT containing a `tenant_id` claim. JWT validation compares it with the configured API tenant on tenant endpoints, preventing a token issued by one tenant environment from being used by another.

For local testing, `src/tenants.json` can hold several descriptors. Change `ActiveTenantId` or set the `Tenancy__ActiveTenantId` environment variable to run the API as another tenant. The migrator always uses its explicit `--tenant` argument. See `documentation/LOCAL_TENANCY.md` for commands and configuration details. Future tenant discovery through subdomains or invitation links can be added at the routing/control-plane boundary without changing module DbContexts or business logic.

---

## 12. What's unfinished, inconsistent, or a known gap

Called out explicitly so you don't copy these patterns by accident, and so you know what to expect if you go looking:

1. **Some scaffold modules still have no API behavior.** Installer-list drift is fixed: both hosts use `SimpliaDz.ModuleCatalog` and the shared tenant catalog. Audit, Accounting, Notifications, and Reporting still consist mostly of scaffolding. Sales currently exposes customers; sales transactions remain future work.
2. **Inventory stock operations have HTTP workflows.** Opening stock, receipts, adjustments, and transfers are available through tenant-scoped endpoints.
3. **Authentication delivery and advanced identity flows are deferred.** Refresh rotation, logout, lockout, and session invalidation work now. Invitation acceptance, password-reset email delivery, email confirmation, MFA, and external identity providers still require product and Notifications/provisioning work. See `AUTHENTICATION.md`.
4. **Dev credentials and secrets are local-only defaults.** The seeded `admin@dev.local` / `Admin123!` account and the local SQL/JWT values must not be reused in production. Production provisioning must provide its own administrator and secrets.
5. **Automated coverage is still partial.** Module-catalog, Organization, Inventory location, Inventory product, and Inventory category tests exist. The Identity session lifecycle has been exercised against the local API and SQL Server, but repeatable Identity/API integration tests should still be added to CI.
6. **The JSON tenant catalog is temporary local infrastructure.** Automated tenant provisioning, a control-plane tenant store, secret management, and tenant-specific routing/invitation links remain future work.

None of these are "wrong" in the sense of breaking the architecture — they're normal signs of a codebase that's mid-build. They're listed here so a future change (yours or Claude's) fixes or extends them deliberately rather than assuming they're finished.

---

## 13. Conventions to follow when adding code

Distilled from the patterns above — follow these to stay consistent with the rest of the codebase:

- **One use case = one folder** under `UseCases/<Domain>/<Commands|Queries>/<Name>/`, containing `<Name>Command.cs` (or `Query.cs`), `<Name>Handler.cs`, and (if it takes input) `<Name>Validator.cs`.
- **Handlers depend on interfaces only**, never on `DbContext` or any concrete Infrastructure type. The interface lives in `UseCases`, next to an `Abstractions/` folder; the implementation lives in `Infrastructure`.
- **Pick repository or service shape per module, not per whim**: aggregate-root persistence with plain EF Core → repository named after the aggregate; wrapping an existing framework subsystem (like ASP.NET Identity) → service interface named after the capability. See [Section 7](#7-repository-pattern-vs-service-pattern--which-to-use-when).
- **Domain rules and invariants live in `Core` entities/value objects**, not in validators and not in handlers. A validator checks *shape* of input (is the email well-formed, is the string non-empty); an entity/value-object constructor or method enforces *business* rules (can't go below zero stock, can't have a duplicate SKU on the same product).
- **Value objects for anything with format rules** (see `ProductName`, `Sku`) — a `readonly record struct` with validation in the constructor, mapped to a plain column via `HasConversion` in the EF configuration.
- **Endpoints stay thin**: bind and validate request shape, call a module use case through Mediator or a module service, then map the result to an HTTP response. Business rules stay in the module rather than in the route.
- **New exception types need a home in the global exception handler** (`MiddlewareExtensions.cs`) or they'll fall through to a generic 500 — don't add a new domain exception without checking whether it needs a status-code mapping.
- **Module-specific DI registrations belong in that module's `IModuleInstaller.Register(...)`**, never added ad hoc to `DependencyInjectionExtensions.cs`.
- **Register a new module once in `SimpliaDz.ModuleCatalog`, update its catalog test, and enable it explicitly per tenant** in `src/tenants.json`. Then add its API use-case scans and endpoint mappings when it has HTTP behavior.
- **Every write endpoint that isn't a public entry point (login, register) should carry `.RequireAuthorization(PermissionNames.<Area>.<Action>)`**. Add new constants to `PermissionNames.All` and add their display metadata to `IdentitySeeder`.
- **Don't create cross-module `ProjectReference`s or `using` statements.** If two modules need to share something, it goes in `SimpliaDz.SharedKernel`, not by referencing one module from another. As of today there is no cross-module communication mechanism (no events, no shared contracts bus) — when a real cross-module workflow is needed (e.g. Sales needing Inventory stock levels), that mechanism will need to be designed; it doesn't exist yet.

---

## 14. Implementation status recap

**Working today:**
- Identity module: login, rotating refresh tokens, logout/session revocation, account lockout, user operations (create, activate/deactivate, role assignment), role management, permission-based authorization, `/auth/me`, and change/forgot/reset password. See [Authentication and session lifecycle](AUTHENTICATION.md).
- Inventory module: create, update, retrieve, search, and deactivate products; manage hierarchical categories and brands; validate brand/category references; expose variants without coupling products to stock quantities; and preserve history through restrictive deletion rules. See [Product Catalog](PRODUCT_CATALOG.md), [Product Categories](PRODUCT_CATEGORIES.md), and [Brands](BRANDS.md).
- Organization module: tenant-isolated profile retrieval and authorized upsert, DZD default currency, validation, permissions, migration, and focused domain/use-case tests.
- Inventory module: manage/deactivate business locations, seeded catalog/reference data, and location-validated stock receipt, adjustment, opening stock, and transfer workflows.
- Purchasing module: manage suppliers; create, place, list, and cancel purchase orders without changing stock; post partial or complete goods receipts atomically with stock movements. See [Suppliers](SUPPLIERS.md), [Purchase Orders](PURCHASE_ORDERS.md), and [Goods Receipts](GOODS_RECEIPTS.md).
- Sales module: create, update, search, retrieve, and deactivate tenant-scoped retail or wholesale customers. See [Customers](CUSTOMERS.md).
- Cross-cutting: Mediator + FluentValidation pipeline, global exception handling, JWT bearer auth, permission-based authorization policies, Docker Compose SQL Server for local dev, a working CLI migrator that is genuinely tenant- and module-aware, and one shared installed-module catalog for the API and migrator.

**Modules that exist (with real code):** Identity, Inventory, Organization, Purchasing, Sales.

**Modules that exist (scaffold only, no domain logic, not reachable from the running API):** Audit, Accounting, Notifications, Reporting.

**Partially implemented / worth knowing about:**
- Purchasing goods receipts update received quantities and Inventory movements in one SQL Server transaction. The optional SQL Server integration test verifies partial/full receipt, duplicate prevention, and rollback after a forced stock write failure; set `SIMPLIADZ_INTEGRATION_CONNECTIONSTRING` to run it.
- Multi-tenancy: each API instance selects one configured tenant database, enforces that tenant's module flags, leaves system endpoints tenant-free, and binds JWTs to their issuing tenant. Local JSON is temporary infrastructure; automated provisioning and a control-plane store do not exist yet.
- Permission names, display labels, categories, and policy registration are centralized in `PermissionNames`.

**Logical next steps** (in a sensible order, not a mandate):
1. Build out the four remaining stub modules as their business workflows become needed.
2. Extend SQL Server integration coverage with simultaneous goods receipts for the same purchase order.
3. Extend test coverage with Identity and API-level authorization/integration tests.
