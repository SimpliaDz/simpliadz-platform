using SimpliaDz.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

var tenantCatalogPath = Environment.GetEnvironmentVariable("SIMPLIADZ_TENANTS_FILE");
if (!string.IsNullOrWhiteSpace(tenantCatalogPath))
    builder.Configuration.AddJsonFile(Path.GetFullPath(tenantCatalogPath), optional: false,
        reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddDataProtection();
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

// Fail during startup if this API instance points at a missing or invalid tenant.
using (var scope = app.Services.CreateScope())
{
    var tenant = scope.ServiceProvider.GetRequiredService<
        SimpliaDz.BuildingBlocks.Tenancy.Abstractions.ITenantContext>();
    app.Logger.LogInformation(
        "API tenant: {TenantId} ({TenantName})",
        tenant.TenantId,
        tenant.Name);
}

app.UseAppMiddleware();
app.MapAppEndpoints();

app.Run();
