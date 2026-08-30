using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Locations.Commands.UpdateLocation;

public sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        LocationValidation.ApplyCommonRules(
            this,
            x => x.Name,
            x => x.Code,
            x => x.Type,
            x => x.Address,
            x => x.Phone);
    }
}
