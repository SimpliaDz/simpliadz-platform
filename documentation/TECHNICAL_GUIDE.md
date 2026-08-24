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
8. [MediatR: how CQRS is wired up](#8-mediatr-how-cqrs-is-wired-up)
9. [FluentValidation: how validation is wired up](#9-fluentvalidation-how-validation-is-wired-up)
10. [Dependency Injection: who registers what, and when](#10-dependency-injection-who-registers-what-and-when)
11. [Multi-tenancy: how it's supposed to work vs. how it works today](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today)
12. [What's unfinished, inconsistent, or a known gap](#12-whats-unfinished-inconsistent-or-a-known-gap)
13. [Conventions to follow when adding code](#13-conventions-to-follow-when-adding-code)
14. [Implementation status recap](#14-implementation-status-recap)

---

## 1. Big picture: what kind of app is this

This is a **modular monolith**: a single deployable ASP.NET Core application (`SimpliaDz.Api`) that is internally split into independent **modules** (Identity, Inventory, and six not-yet-implemented ones), each structured using **Clean Architecture** layers, with **CQRS via MediatR** inside each module.

Why this combination, in plain terms:

- **Modular monolith** (as opposed to microservices): all the code runs in one process and one deployment, but it's organized as if it *could* be split into services later. Each module owns its own database schema and never reaches into another module's tables directly. You get most of the maintainability benefits of microservices (isolation, clear boundaries) without the operational cost (network calls, distributed transactions, separate deployments) — a reasonable choice for a product that isn't yet at a scale that needs real microservices.
- **Clean Architecture** inside each module: the code is arranged in layers where the "inner" layer (business rules) never depends on the "outer" layer (database, web framework). This means you can change the database or the web framework without rewriting business logic, and you can unit-test business logic without spinning up a real database.
- **CQRS with MediatR**: instead of big "service" classes with a dozen methods, each individual use case (e.g. "create a user", "get a role by id") is its own small class (a `Command` or `Query` + its `Handler`). This keeps each use case easy to find, read, and test in isolation, and gives you one consistent place (a MediatR pipeline) to bolt on cross-cutting behavior like validation.

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
    ├── Inventory/                  # ACTIVE — products, variants, brands, categories, stock.
    └── Audit | Sales | Purchasing | Accounting | Notifications | Reporting/   # SCAFFOLDED, NOT IMPLEMENTED.
tests/                              # Empty — no test project exists yet.
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
- `Extensions/DependencyInjectionExtensions.cs` — registers every module's services, MediatR, FluentValidation, and authorization policies.
- `Extensions/AuthenticationExtensions.cs` — configures JWT Bearer authentication.
- `Extensions/MiddlewareExtensions.cs` — configures the global exception handler, Swagger, and the auth middleware pipeline.
- `Extensions/EndpointExtensions.cs` — maps every module's HTTP endpoint groups onto the app.
- `Endpoints/Identity/*`, `Endpoints/Inventory/*` — the actual Minimal API route definitions (thin — they just call MediatR and return a result, see the walkthroughs below).
- `Configuration/JwtOptions.cs` — strongly-typed JWT settings bound from `appsettings.json`.

### `SimpliaDz.BuildingBlocks` — shared cross-cutting plumbing

Code every module can depend on, but which is **not** business logic:

- `Exceptions/NotFoundException.cs` — a generic "entity not found" exception any module can throw; the API's global exception handler knows how to turn it into an HTTP 404.
- `Mediation/ValidationBehavior.cs` — the MediatR pipeline step that runs FluentValidation before every handler (details in [Section 9](#9-fluentvalidation-how-validation-is-wired-up)).
- `Modules/Abstractions/IModuleInstaller.cs` — the contract every module implements to plug itself into the host (details in [Section 3](#3-the-modular-monolith-how-modules-are-organized)).
- `Tenancy/` — `ITenantContext`, `ITenantResolver`, `ITenantStore`, `TenancyOptions`, `ConfigurationTenantStore`, `TenantContext`, `CliTenantResolver` — the multi-tenancy abstractions (details in [Section 11](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today)).

### `SimpliaDz.SharedKernel`

Exists as an empty class-library project (just the `.csproj`, no `.cs` files). It's a placeholder for domain primitives that might eventually be shared *between modules* (e.g. a `Money` value object used by both Sales and Accounting). Every module already has a `ProjectReference` to it, so when something genuinely shared shows up, this is where it goes — don't put it inside one module's `Core` and reference it from another module.

### `SimpliaDz.ModuleCatalog`

A small composition project, not a business module. It references every module's Infrastructure project and exposes the authoritative `ModuleCatalog.GetModules()` list of installed `IModuleInstaller` implementations. Both the API and migrator reference it, which prevents them from maintaining separate installer lists. Tenant configuration still decides which entries from that list each host activates. See [Module Catalog](MODULE_CATALOG.md) for its dependency rationale and maintenance steps.

### `SimpliaDz.DatabaseMigrator`

A separate console app (not the web API) whose only job is: for a given `--tenant <id>`, figure out which modules are enabled for that tenant, and run `MigrateAsync` + `SeedAsync` for each of them. This is the tool you actually run to create/update a tenant's database. See [Section 3](#3-the-modular-monolith-how-modules-are-organized) and [Section 11](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today) for exactly how it decides what to run.

### `Modules/<ModuleName>/`

Each module is four separate class-library projects — see [Section 4](#4-clean-architecture-inside-a-module) for what goes in each and why. Two modules (**Identity**, **Inventory**) have real code. Six (**Audit, Sales, Purchasing, Accounting, Notifications, Reporting**) are scaffolds only — see [Section 12](#12-whats-unfinished-inconsistent-or-a-known-gap).

### `tests/`

Contains a single empty placeholder file. No test project exists yet — there is currently no automated test coverage anywhere in the solution.

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

`SimpliaDz.ModuleCatalog/ModuleCatalog.cs` is the one place that constructs all eight installed installers (Identity, Inventory, Sales, Purchasing, Accounting, Notifications, Reporting, Audit). The API's `ModuleRegistry` delegates to this catalog, and the migrator registers its `IModuleInstaller` instances from the same catalog.

Each host then applies its tenant configuration:

- The API registers all installed Infrastructure modules once at startup. For every request it resolves a tenant from `X-Tenant-Id`, creates its scoped `ITenantContext`, uses that context's connection string for EF Core, and returns 404 for an endpoint belonging to a module disabled for that tenant.
- The migrator resolves the selected `--tenant`, then registers, migrates, and seeds only modules for which `tenant.IsModuleEnabled(installer.ModuleName)` is true.

This removes installer-list drift. It does **not** make scaffolded modules functional: a module still needs handlers, endpoint mappings, and its UseCases assembly included in the API scans before it exposes behavior.

**How to add a new module** (from `CLAUDE.md`, confirmed against the code):

1. Create the four projects: `SimpliaDz.<Module>.Core`, `.UseCases`, `.Infrastructure`, `.Contracts`.
2. In `Infrastructure`, implement `IModuleInstaller`.
3. Add the Infrastructure project reference and installer to `SimpliaDz.ModuleCatalog/ModuleCatalog.cs`.
4. Enable the module for the relevant tenant(s) in each host's `appsettings.json` under `Tenancy:Tenants:<tenant>:Modules`.
5. Update `tests/SimpliaDz.ModuleCatalog.Tests` with the expected module name.
6. Add its `UseCases` assembly to the MediatR/FluentValidation assembly-scan list in `DependencyInjectionExtensions.cs`.
7. Add its endpoint-mapping call in `EndpointExtensions.cs`.

---

## 4. Clean Architecture inside a module

Every module (active or stub) is split into four projects, and the **dependency arrows only point inward**:

```
   Api  ─────────────────────────────────────────────►  (calls into UseCases via MediatR)
    │
    ▼
Infrastructure ──► UseCases ──► Contracts
    │                 │
    └────────────────►Core◄────────────────────────────  (Core depends on nothing)
```

| Layer | What lives here | What it may depend on |
|---|---|---|
| **`<Module>.Core`** | Domain entities, value objects, domain exceptions. The actual business rules. | Nothing (no NuGet packages). Framework types are allowed only when the domain itself needs them — e.g. Identity's entities extend ASP.NET Core Identity's `IdentityUser<Guid>`. |
| **`<Module>.UseCases`** | MediatR `Command`/`Query` + `Handler` + FluentValidation `Validator`, and the **interfaces** (repository or service abstractions) that handlers depend on. | MediatR, FluentValidation, `BuildingBlocks`, its own `Core`. **Never** EF Core — this layer must not know the database exists. |
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

**2. API endpoint** (`src/SimpliaDz.Api/Endpoints/Identity/UserEndpoints.cs`) — binds the request, sends it through MediatR, returns the result. No business logic here:
```csharp
group.MapPost("/", async ([FromBody] CreateUserRequest request, [FromServices] ISender sender, CancellationToken ct) =>
{
    var id = await sender.Send(
        new CreateUserCommand(request.Email, request.FirstName, request.LastName, request.Password, request.Roles), ct);
    return Results.Created($"/users/{id}", new { id });
}).RequireAuthorization("users.create");
```
`RequireAuthorization("users.create")` — the policy name *is* the permission string. See [Section 11's sibling topic] — actually see the permission-policy explanation later in this section.

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
1. Permissions are seeded into the database by `IdentitySeeder` (12 rows: `users.view`, `users.create`, `users.edit`, `users.delete`, `roles.view`, `roles.manage`, `permissions.view`, `permissions.manage`, `products.view`, `products.create`, `products.edit`, `products.delete`) and an `Admin` role is granted all of them.
2. On login, `JwtTokenGenerator` puts one JWT claim per effective permission into the token:
   ```csharp
   foreach (var permission in permissions)
       claims.Add(new Claim("permission", permission));
   ```
3. At startup, `DependencyInjectionExtensions.cs` turns every permission string into its own ASP.NET Core authorization **policy**, named after the permission itself:
   ```csharp
   foreach (var permission in AllPermissions)
       options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission));
   ```
4. Endpoints call `.RequireAuthorization("users.create")` — the string is both the policy name and the exact claim value it checks for.

Dev-seeded admin login: `admin@dev.local` / `Admin123!` (see [Section 12](#12-whats-unfinished-inconsistent-or-a-known-gap) for a note on this).

---

## 6. Walkthrough #2: Inventory module, "CreateProduct" command

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
    Task AddAsync(Product product, CancellationToken ct);
    Task UpdateAsync(Product product, CancellationToken ct);
    Task DeleteAsync(Product product, CancellationToken ct);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```
`CreateProductHandler` depends only on this interface: checks `ExistsByCodeAsync`, constructs a `Product`, calls `AddAsync` + `SaveChangesAsync`.

**Infrastructure** — the EF Core implementation (`src/Modules/Inventory/SimpliaDz.Inventory.Infrastructure/Data/Repositories/EfProductRepository.cs`):
```csharp
internal sealed class EfProductRepository : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct);
    // AddAsync/UpdateAsync/DeleteAsync/ExistsByCodeAsync/SaveChangesAsync follow the same one-liner-over-EF shape
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

**API endpoint** (`src/SimpliaDz.Api/Endpoints/Inventory/ProductEndpoints.cs`): `POST /products` — sends `CreateProductCommand` through MediatR. **Has no `.RequireAuthorization(...)` call** — this is a known, documented gap (see [Section 12](#12-whats-unfinished-inconsistent-or-a-known-gap)), not an oversight to copy.

---

## 7. Repository pattern vs. Service pattern — which to use when

You'll notice **Identity uses "services"** (`IIdentityUserService`, `IIdentityAuthService`, `IIdentityRoleService`, `IPermissionService`) while **Inventory uses a "repository"** (`IProductRepository`). Both are valid implementations of the *same* rule (`UseCases` defines the interface, `Infrastructure` implements it against EF Core) — the difference is just the shape of the abstraction, chosen per module:

- **Repository pattern** (Inventory): one interface per aggregate root, exposing persistence-shaped verbs (`GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `SaveChangesAsync`). Reach for this when the module is plain EF Core domain persistence — you're the one designing the whole data-access surface.
- **Service pattern** (Identity): one interface per capability, exposing workflow-shaped verbs (`CreateUserAsync`, `AssignRolesAsync`, `GetEffectivePermissionsAsync`). Reach for this when you're wrapping an existing framework API that already has its own manager/store abstraction (here, ASP.NET Core Identity's `UserManager<T>`/`RoleManager<T>`) — a generic repository would just be an awkward re-wrap of something that already has a good API.

**Rule of thumb for new modules:** if you're persisting your own domain aggregate with plain EF Core, use a repository named after the aggregate (`IOrderRepository`, `IInvoiceRepository`). If you're wrapping a third-party subsystem that already exposes its own manager/store API, wrap it in a service interface instead of forcing a repository shape onto it.

---

## 8. MediatR: how CQRS is wired up

**Registration** (`src/SimpliaDz.Api/Extensions/DependencyInjectionExtensions.cs`):
```csharp
var useCaseAssemblies = new[]
{
    typeof(SimpliaDz.Inventory.UseCases.AssemblyMarker).Assembly,
    typeof(SimpliaDz.Identity.UseCases.AssemblyMarker).Assembly
};
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(useCaseAssemblies));
```
MediatR scans these assemblies for every `IRequestHandler<,>` implementation and registers them. **Only the two active modules' `UseCases` assemblies are scanned** — a new module's handlers won't be found until its assembly is added to this array (see [Section 3](#3-the-modular-monolith-how-modules-are-organized), step 6).

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

**Registration** — same two assemblies as MediatR:
```csharp
services.AddValidatorsFromAssemblies(useCaseAssemblies);
```
This finds every `AbstractValidator<T>` and registers it as `IValidator<T>`.

**How it actually runs** (`src/SimpliaDz.BuildingBlocks/Mediation/ValidationBehavior.cs`):
```csharp
public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    if (_validators.Any())
    {
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);
    }
    return await next();
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
2. Read the tenant configuration section; no tenant connection string is selected at startup. See [Section 11](#11-multi-tenancy-how-its-supposed-to-work-vs-how-it-works-today).
3. Register the HTTP tenancy services: configuration-backed tenant store, scoped tenant context, and `HttpTenantResolver`.
4. Get all installed modules from `ModuleRegistry`/`ModuleCatalog` and call `installer.Register(...)`. Each module's `DbContext` resolves `ITenantContext.ConnectionString` when the context is created for a request, rather than capturing a connection string at startup.
5. `AddMediatR` + `AddValidatorsFromAssemblies` over the active modules' `UseCases` assemblies.
6. `AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))`.
7. `AddAuthorization(...)` — builds one policy per permission string.

**Step 2 — `AddJwtAuthentication`** (`src/SimpliaDz.Api/Extensions/AuthenticationExtensions.cs`): binds+validates `JwtOptions` (fails fast at startup if `SigningKey` is missing or under 32 characters), then `AddAuthentication().AddJwtBearer(...)`.

**Lifetimes used throughout:**
| Service kind | Lifetime | Why |
|---|---|---|
| `DbContext` (each module's) | Scoped (EF Core default via `AddDbContext`) | One instance per HTTP request / unit of work. |
| Repositories / application services (`EfProductRepository`, `IdentityUserService`, ...) | Scoped | They hold a scoped `DbContext`, so they must match its lifetime. |
| MediatR handlers | Transient (MediatR default) | Cheap, stateless, created per request. |
| `IValidator<T>` | Transient (FluentValidation default) | Same reasoning. |
| `IPipelineBehavior<,>` (`ValidationBehavior`) | Transient | Registered once as an open generic, applies to every request type. |
| `ITenantStore` | Singleton | Config-backed, safe to share app-wide. |
| `ITenantContext` | Scoped | Resolved once per scope from `ITenantResolver` + `ITenantStore`. |

**Rule for new code:** register your module's own services **inside your module's `IModuleInstaller.Register(...)`**, not in `DependencyInjectionExtensions.cs`. That file should stay generic (loop over modules, wire MediatR/FluentValidation/authorization) and never accumulate module-specific `AddScoped` calls directly.

---

## 11. Multi-tenancy: how it's supposed to work vs. how it works today

**The abstractions** (`src/SimpliaDz.BuildingBlocks/Tenancy/`):
```csharp
public interface ITenantResolver { string ResolveTenantId(); }
public interface ITenantStore    { TenantDescriptor GetTenant(string tenantId); }
public interface ITenantContext  { string TenantId; string Name; string ConnectionString; bool IsModuleEnabled(string moduleName); }
```
Config shape (`appsettings.json`):
```json
"Tenancy": {
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

1. Every API request must send exactly one `X-Tenant-Id` header, for example `X-Tenant-Id: dev`.
2. `HttpTenantResolver` reads this header. `AddTenancyFromConfiguration` uses it to create one scoped `ITenantContext` from the configured tenant descriptor.
3. Middleware resolves that context before authentication and endpoints execute. Unknown tenants or missing/multiple headers fail as a 400 response.
4. Each module's `AddDbContext` registration obtains `ITenantContext.ConnectionString` when EF Core creates the context, so the same API process can serve separate tenant databases.
5. Endpoint groups carry `ModuleEndpointMetadata` (`Identity` or `Inventory` today). The middleware returns 404 when the resolved tenant has that module disabled.
6. Login issues a JWT containing a `tenant_id` claim. JWT validation compares it with the request tenant, preventing a token issued for one tenant from being used with another tenant header.

This fixes request-level tenant selection for the HTTP API. The chosen transport is a header because it is easy to use with the existing single-host local deployment. A future subdomain or custom-domain strategy can replace only `HttpTenantResolver`; the rest of the tenant context and database behavior remains unchanged.

---

## 12. What's unfinished, inconsistent, or a known gap

Called out explicitly so you don't copy these patterns by accident, and so you know what to expect if you go looking:

1. **Scaffold modules still have no API behavior.** Installer-list drift is fixed: both hosts now use `SimpliaDz.ModuleCatalog`, and the API resolves tenant flags per request. Audit, Sales, Purchasing, Accounting, Notifications, and Reporting still consist mostly of scaffolding; enabling one only registers its infrastructure. It remains unreachable until its use cases and endpoints are implemented and mapped. The API and migrator each keep their own host configuration, so their enabled-module flags can still differ intentionally or accidentally (the checked-in `dev` settings currently differ for Audit).
2. **The permission catalog is defined twice, by hand, with no shared source of truth:**
   - `IdentitySeeder.PermissionCatalog` (`src/Modules/Identity/SimpliaDz.Identity.Infrastructure/Data/Seeders/IdentitySeeder.cs`) — the list that gets inserted into the database.
   - `DependencyInjectionExtensions.AllPermissions` (`src/SimpliaDz.Api/Extensions/DependencyInjectionExtensions.cs`) — the list that becomes authorization policies.
   Add a permission to one list and forget the other, and you'll get either a missing DB row or `.RequireAuthorization("new.permission")` failing because no such policy exists. If you add a permission, **update both lists**, or better, consider consolidating them into one shared constant when you touch this area.
3. **`AddAuthorization()` is called twice** — once with the permission-policy loop in `DependencyInjectionExtensions.cs`, once bare (no options) in `AuthenticationExtensions.AddJwtAuthentication`. Harmless (ASP.NET Core stacks the configuration), but redundant; the second call could be removed.
4. **Inventory's `ProductVariant` has no CRUD beyond create.** `IProductRepository` already defines `UpdateAsync`/`DeleteAsync`/`GetByIdAsync`, but only a `CreateProduct` command/handler/endpoint exists — no update, delete, get, or list use case, and `ProductEndpoints.cs` only maps `POST /products`. The repository is ahead of the use-case layer here.
5. **`POST /products` has no authorization policy** — every Identity endpoint requires a permission claim, but this one doesn't yet. This is a known, called-out gap (also noted in `CLAUDE.md`), not something to copy for new endpoints — new write endpoints should have a `.RequireAuthorization("<permission>")` call.
6. **`IStockService` is a defined-but-dead abstraction** (`src/Modules/Inventory/SimpliaDz.Inventory.UseCases/Services/IStockService.cs`) — declared, never implemented, never registered in `InventoryModuleInstaller`. `StockItem`/`StockMovement` exist in `Core` and are only actually touched by the seeder today.
7. **Dev admin credentials are committed in plaintext** in `IdentitySeeder.cs` (`admin@dev.local` / `Admin123!`), and `SeedAsync` runs unconditionally with no environment guard. Fine for local dev; don't let this seeder run unguarded against anything but a local/dev database.
8. **Test coverage is still minimal.** `tests/SimpliaDz.ModuleCatalog.Tests` now protects the installed-module list, but there are still no tests for Identity, Inventory, authorization, tenancy, migrations, or API endpoints.
9. **Small, low-risk uncommitted config tweaks** are currently sitting in the working tree (`docker-compose.yml` timezone/volume/port-default changes, a `global.json` SDK bump to `10.0.400`, and a new `"amin"` dev tenant added to `SimpliaDz.DatabaseMigrator/appsettings.json`) — not a code issue, just worth knowing they're uncommitted if you're about to `git status`/`git diff` and wonder what's there.

None of these are "wrong" in the sense of breaking the architecture — they're normal signs of a codebase that's mid-build. They're listed here so a future change (yours or Claude's) fixes or extends them deliberately rather than assuming they're finished.

---

## 13. Conventions to follow when adding code

Distilled from the patterns above — follow these to stay consistent with the rest of the codebase:

- **One use case = one folder** under `UseCases/<Domain>/<Commands|Queries>/<Name>/`, containing `<Name>Command.cs` (or `Query.cs`), `<Name>Handler.cs`, and (if it takes input) `<Name>Validator.cs`.
- **Handlers depend on interfaces only**, never on `DbContext` or any concrete Infrastructure type. The interface lives in `UseCases`, next to an `Abstractions/` folder; the implementation lives in `Infrastructure`.
- **Pick repository or service shape per module, not per whim**: aggregate-root persistence with plain EF Core → repository named after the aggregate; wrapping an existing framework subsystem (like ASP.NET Identity) → service interface named after the capability. See [Section 7](#7-repository-pattern-vs-service-pattern--which-to-use-when).
- **Domain rules and invariants live in `Core` entities/value objects**, not in validators and not in handlers. A validator checks *shape* of input (is the email well-formed, is the string non-empty); an entity/value-object constructor or method enforces *business* rules (can't go below zero stock, can't have a duplicate SKU on the same product).
- **Value objects for anything with format rules** (see `ProductName`, `Sku`) — a `readonly record struct` with validation in the constructor, mapped to a plain column via `HasConversion` in the EF configuration.
- **Endpoints stay thin**: bind the request, `sender.Send(...)`, map the result to an HTTP response. No business logic, no direct repository/service calls, no manual validation — that's what the MediatR pipeline + `ValidationBehavior` already does.
- **New exception types need a home in the global exception handler** (`MiddlewareExtensions.cs`) or they'll fall through to a generic 500 — don't add a new domain exception without checking whether it needs a status-code mapping.
- **Module-specific DI registrations belong in that module's `IModuleInstaller.Register(...)`**, never added ad hoc to `DependencyInjectionExtensions.cs`.
- **Register a new module once in `SimpliaDz.ModuleCatalog`, update its catalog test, and enable it explicitly per tenant** in each host's `appsettings.json`. Then add its API use-case scans and endpoint mappings when it has HTTP behavior.
- **Every write endpoint that isn't a public entry point (login, register) should carry `.RequireAuthorization("<permission>")`** — and if it's a new permission, add it to *both* permission lists (see gap #3 above) until that's consolidated.
- **Don't create cross-module `ProjectReference`s or `using` statements.** If two modules need to share something, it goes in `SimpliaDz.SharedKernel`, not by referencing one module from another. As of today there is no cross-module communication mechanism (no events, no shared contracts bus) — when a real cross-module workflow is needed (e.g. Sales needing Inventory stock levels), that mechanism will need to be designed; it doesn't exist yet.

---

## 14. Implementation status recap

**Working today:**
- Identity module: login (JWT issuance), user CRUD-ish operations (create, activate/deactivate, role assignment), role management, permission-based authorization, `/auth/me`, change/forgot/reset password.
- Inventory module: create a product (with brand/category assignment and variants), seeded reference data (locations, brands, categories, 4 products/8 variants, partial initial stock).
- Cross-cutting: MediatR + FluentValidation pipeline, global exception handling, JWT bearer auth, permission-based authorization policies, Docker Compose SQL Server for local dev, a working CLI migrator that is genuinely tenant- and module-aware, and one shared installed-module catalog for the API and migrator.

**Modules that exist (with real code):** Identity, Inventory.

**Modules that exist (scaffold only, no domain logic, not reachable from the running API):** Audit, Sales, Purchasing, Accounting, Notifications, Reporting.

**Partially implemented / worth knowing about:**
- Inventory: only `CreateProduct` exists; no update/delete/get/list use cases despite the repository already supporting them; no stock-movement workflow wired up (`IStockService` is unimplemented); `POST /products` has no authorization policy yet.
- Multi-tenancy: API requests resolve tenants from `X-Tenant-Id`, select their own database connection string, enforce module flags, and bind JWTs to their issuing tenant. There is not yet an alternative subdomain/custom-domain tenant transport.
- Permission catalog: maintained by hand in two separate places.

**Logical next steps** (in a sensible order, not a mandate):
1. Decide whether the six stub modules should be built out one at a time, or trimmed from the solution until they're needed — right now they add scaffolding weight with zero function.
2. Round out Inventory with the missing product use cases (get/list/update/delete) and an authorization policy on `POST /products`, then decide how/if `IStockService` gets implemented and wired into a real workflow.
3. Consolidate the permission catalog into a single shared source (e.g. a constants class in `BuildingBlocks` or `Identity.Contracts`) referenced by both the seeder and the policy registration.
4. Extend the new test project beyond catalog coverage, starting with Identity and Inventory use cases and API authorization behavior.
