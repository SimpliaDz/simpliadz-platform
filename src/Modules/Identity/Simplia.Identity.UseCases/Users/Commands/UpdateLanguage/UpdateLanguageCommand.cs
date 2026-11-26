using Mediator;
using Simplia.Identity.Contracts.Users;

namespace Simplia.Identity.UseCases.Users.Commands.UpdateLanguage;

public sealed record UpdateLanguageCommand(Guid UserId, string LanguageCode) : IRequest<UserResponse>;
