using MediatR;
using SimpliaDz.Organization.Contracts;

namespace SimpliaDz.Organization.UseCases.Commands.UpdateOrganization;

public sealed record UpdateOrganizationCommand(
    string Name,
    string? LegalName,
    string? Phone,
    string? Email,
    string? Address,
    string? CurrencyCode,
    string? DefaultLanguage,
    bool IsActive) : IRequest<OrganizationResponse>;
