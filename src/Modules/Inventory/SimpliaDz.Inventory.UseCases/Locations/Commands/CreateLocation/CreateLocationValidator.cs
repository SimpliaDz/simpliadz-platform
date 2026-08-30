using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Locations.Commands.CreateLocation;

public sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator()
    {
        LocationValidation.ApplyCommonRules(
            this,
            x => x.Name,
            x => x.Code,
            x => x.Type,
            x => x.Address,
            x => x.Phone);
    }
}
