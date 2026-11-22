using Mediator;
using SimpliaDz.Identity.Contracts.Users;
using SimpliaDz.Identity.UseCases.Users.Abstractions;

namespace SimpliaDz.Identity.UseCases.Users.Commands.UpdateLanguage;

public sealed class UpdateLanguageHandler(IIdentityUserService users)
    : IRequestHandler<UpdateLanguageCommand, UserResponse>
{
    public async ValueTask<UserResponse> Handle(UpdateLanguageCommand request, CancellationToken ct) =>
        await users.UpdateLanguageAsync(request.UserId, request.LanguageCode.Trim().ToLowerInvariant(), ct);
}
