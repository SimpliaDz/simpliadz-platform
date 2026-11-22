using Mediator;
using SimpliaDz.Identity.Contracts.Users;

namespace SimpliaDz.Identity.UseCases.Users.Commands.UpdateLanguage;

public sealed record UpdateLanguageCommand(Guid UserId, string LanguageCode) : IRequest<UserResponse>;
