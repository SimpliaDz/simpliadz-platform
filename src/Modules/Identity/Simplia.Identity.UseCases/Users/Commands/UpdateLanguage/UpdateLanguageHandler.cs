using Mediator;
using Simplia.Identity.Contracts.Users;
using Simplia.Identity.UseCases.Users.Abstractions;

namespace Simplia.Identity.UseCases.Users.Commands.UpdateLanguage;

public sealed class UpdateLanguageHandler(IIdentityUserService users)
    : IRequestHandler<UpdateLanguageCommand, UserResponse>
{
    public async ValueTask<UserResponse> Handle(UpdateLanguageCommand request, CancellationToken ct) =>
        await users.UpdateLanguageAsync(request.UserId, request.LanguageCode.Trim().ToLowerInvariant(), ct);
}
