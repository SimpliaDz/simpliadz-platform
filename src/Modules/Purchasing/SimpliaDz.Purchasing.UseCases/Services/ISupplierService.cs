namespace SimpliaDz.Purchasing.UseCases.Services;

public interface ISupplierService
{
    Task<SupplierDto> CreateAsync(string name, string? phone, string? email,
        string? address, string? notes, CancellationToken ct);
    Task<SupplierDto> UpdateAsync(Guid id, string name, string? phone, string? email,
        string? address, string? notes, CancellationToken ct);
    Task DeactivateAsync(Guid id, CancellationToken ct);
    Task<SupplierDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SupplierDto>> SearchAsync(string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct);
}

public sealed record SupplierDto(Guid Id, string Name, string? Phone, string? Email,
    string? Address, string? Notes, bool IsActive, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
