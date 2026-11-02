using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using SimpliaDz.BuildingBlocks.Modules.Abstractions;
using SimpliaDz.BuildingBlocks.Tenancy;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.UseCases.Services;
using SimpliaDz.SharedKernel.Authorization;

namespace SimpliaDz.Api.Endpoints.Sales;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customers")
            .WithTags("Customers")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapGet("/", async (string? search, string? customerType, bool? isActive,
            int? page, int? pageSize, [FromServices] ICustomerService customers,
            CancellationToken ct) =>
        {
            var selectedPage = page ?? 1;
            var selectedSize = pageSize ?? 50;
            var errors = new Dictionary<string, string[]>();
            if (selectedPage < 1 || selectedSize is < 1 or > 100 ||
                selectedPage > int.MaxValue / selectedSize)
                errors["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."];
            if (search?.Length > 150) errors["search"] = ["Search cannot exceed 150 characters."];
            CustomerType? parsedType = null;
            if (!string.IsNullOrWhiteSpace(customerType))
            {
                if (!IsNamedType(customerType))
                    errors["customerType"] = ["Customer type must be Retail or Wholesale."];
                else parsedType = Enum.Parse<CustomerType>(customerType.Trim(), true);
            }
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await customers.SearchAsync(search, parsedType, isActive,
                selectedPage, selectedSize, ct));
        }).RequireAuthorization(PermissionNames.Customers.View);

        group.MapGet("/{id:guid}", async (Guid id,
            [FromServices] ICustomerService customers, CancellationToken ct) =>
            Results.Ok(await customers.GetByIdAsync(id, ct)))
            .RequireAuthorization(PermissionNames.Customers.View);

        group.MapPost("/", async ([FromBody] CustomerRequest request,
            [FromServices] ICustomerService customers, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var type = ParseType(request.CustomerType);
            var customer = await customers.CreateAsync(request.Name, request.Phone,
                request.Email, request.Address, type, request.Notes, ct);
            return Results.Created($"/customers/{customer.Id}", customer);
        }).RequireAuthorization(PermissionNames.Customers.Manage);

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] CustomerRequest request,
            [FromServices] ICustomerService customers, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await customers.UpdateAsync(id, request.Name, request.Phone,
                request.Email, request.Address, ParseType(request.CustomerType), request.Notes, ct));
        }).RequireAuthorization(PermissionNames.Customers.Manage);

        group.MapPut("/{id:guid}/deactivate", async (Guid id,
            [FromServices] ICustomerService customers, CancellationToken ct) =>
        {
            await customers.DeactivateAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.Customers.Manage);

        return app;
    }

    private static CustomerType ParseType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? CustomerType.Retail :
        Enum.Parse<CustomerType>(value.Trim(), true);

    private static bool IsNamedType(string? value) =>
        Enum.GetNames<CustomerType>().Any(name =>
            string.Equals(name, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string[]> Validate(CustomerRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = ["Customer name is required."];
        else if (request.Name.Trim().Length > 150)
            errors["name"] = ["Customer name cannot exceed 150 characters."];
        if (request.Phone?.Trim().Length > 50)
            errors["phone"] = ["Phone cannot exceed 50 characters."];
        if (request.Address?.Trim().Length > 500)
            errors["address"] = ["Address cannot exceed 500 characters."];
        if (request.Notes?.Trim().Length > 2000)
            errors["notes"] = ["Notes cannot exceed 2000 characters."];
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim();
            if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) ||
                parsed.Address != email)
                errors["email"] = ["A valid email of at most 254 characters is required."];
        }
        if (!string.IsNullOrWhiteSpace(request.CustomerType) &&
            !IsNamedType(request.CustomerType))
            errors["customerType"] = ["Customer type must be Retail or Wholesale."];
        return errors;
    }
}

public sealed record CustomerRequest(string Name, string? Phone, string? Email,
    string? Address, string? CustomerType, string? Notes);
