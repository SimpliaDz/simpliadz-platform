using Microsoft.AspNetCore.Mvc;
using Simplia.BuildingBlocks.Modules.Abstractions;
using Simplia.BuildingBlocks.Tenancy;
using Simplia.Sales.UseCases.Services;
using Simplia.SharedKernel.Authorization;

namespace Simplia.Api.Endpoints.Sales;

public static class CashRegisterEndpoints
{
    public static IEndpointRouteBuilder MapCashRegisterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cash-registers")
            .WithTags("Cash Registers")
            .WithMetadata(new TenantRequiredMetadata())
            .WithMetadata(new ModuleEndpointMetadata("Sales"));

        group.MapGet("/", async (Guid? locationId,
            [FromServices] ICashRegisterService registers, CancellationToken ct) =>
            Results.Ok(await registers.ListAsync(locationId, ct)))
            .RequireAuthorization(PermissionNames.CashRegisters.View);

        group.MapPost("/", async ([FromBody] CashRegisterRequest request,
            [FromServices] ICashRegisterService registers, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var result = await registers.CreateAsync(request.LocationId,
                request.Code, request.Name, ct);
            return Results.Created($"/cash-registers/{result.Id}", result);
        }).RequireAuthorization(PermissionNames.CashRegisters.Manage);

        group.MapPut("/{id:guid}", async (Guid id,
            [FromBody] UpdateCashRegisterRequest request,
            [FromServices] ICashRegisterService registers, CancellationToken ct) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await registers.UpdateAsync(id, request.Code,
                request.Name, ct));
        }).RequireAuthorization(PermissionNames.CashRegisters.Manage);

        group.MapPut("/{id:guid}/deactivate", async (Guid id,
            [FromServices] ICashRegisterService registers, CancellationToken ct) =>
        {
            await registers.DeactivateAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(PermissionNames.CashRegisters.Manage);

        return app;
    }

    private static Dictionary<string, string[]> Validate(CashRegisterRequest request)
    {
        var errors = Validate(new UpdateCashRegisterRequest(request.Code, request.Name));
        if (request.LocationId == Guid.Empty) errors["locationId"] = ["Location is required."];
        return errors;
    }

    private static Dictionary<string, string[]> Validate(UpdateCashRegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 40)
            errors["code"] = ["Code is required and cannot exceed 40 characters."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            errors["name"] = ["Name is required and cannot exceed 100 characters."];
        return errors;
    }
}

public sealed record CashRegisterRequest(Guid LocationId, string Code, string Name);
public sealed record UpdateCashRegisterRequest(string Code, string Name);
