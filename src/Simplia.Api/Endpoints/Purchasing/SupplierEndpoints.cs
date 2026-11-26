using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Purchasing.UseCases.Services;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Purchasing;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/suppliers")
            .WithTags("Suppliers")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Purchasing"));

        group.MapGet("/", async (string? search, bool? isActive, int? page, int? pageSize,
            [FromServices] ISupplierService suppliers, CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (search?.Length > 150) errors["search"] = ["Search cannot exceed 150 characters."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await suppliers.SearchAsync(search, isActive,
                selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Suppliers.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] ISupplierService suppliers, CancellationToken ct) =>
            Results.Ok(await suppliers.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Suppliers.View);

        group.MapPost("/", async ([FromBody] SupplierRequest request,
            [FromServices] ISupplierService suppliers, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var supplier = await suppliers.CreateAsync(request.Name, request.Phone,
                request.Email, request.Address, request.Notes, ct);
            return Results.Created($"/suppliers/{supplier.Id}", supplier);
        }).RequireAuthorization(PermissionNames.Suppliers.Manage);

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] SupplierRequest request,
            [FromServices] ISupplierService suppliers, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await suppliers.UpdateAsync(id, request.Name, request.Phone,
                request.Email, request.Address, request.Notes, ct));
        }).RequireAuthorization(PermissionNames.Suppliers.Manage);

        group.MapPut("/{id:guid}/deactivate", async (Guid id,
            [FromServices] ISupplierService suppliers, CancellationToken ct) =>
        {
            await suppliers.DeactivateAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Suppliers.Manage);

        return app;
    }

    private static Dictionary<string, string[]> Validate(SupplierRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = ["Supplier name is required."];
        else if (request.Name.Trim().Length > 150)
            errors["name"] = ["Supplier name cannot exceed 150 characters."];
        if (request.Phone?.Trim().Length > 50) errors["phone"] = ["Phone cannot exceed 50 characters."];
        if (request.Address?.Trim().Length > 500) errors["address"] = ["Address cannot exceed 500 characters."];
        if (request.Notes?.Trim().Length > 2000) errors["notes"] = ["Notes cannot exceed 2000 characters."];
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) ||
                parsed.Address != email)
                errors["email"] = ["A valid email of at most 254 characters is required."];
        }
        return errors;
    }
}

public sealed record SupplierRequest(string Name, string? Phone, string? Email,
    string? Address, string? Notes);
