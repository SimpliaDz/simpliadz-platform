using Simplia.Sales.Core.Entities;

namespace Simplia.Sales.UseCases.Services;

public interface ICustomerService
{
    Task<CustomerDto> CreateAsync(string name, string? phone, string? email, string? address,
        CustomerType customerType, string? notes, CancellationToken ct);
    Task<CustomerDto> UpdateAsync(Guid id, string name, string? phone, string? email,
        string? address, CustomerType customerType, string? notes, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
    Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CustomerDto>> SearchAsync(string? search, CustomerType? customerType,
        bool? isActive, int page, int pageSize, CancellationToken ct);
}

public sealed record CustomerDto(Guid Id, string Name, string? Phone, string? Email,
    string? Address, string CustomerType, string? Notes, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
