using Afisha.Api.Contracts;
using FluentValidation;

namespace Afisha.Api.Validation;

public sealed class ReservationRequestValidator : AbstractValidator<ReservationRequest>
{
    public ReservationRequestValidator()
    {
        RuleFor(x => x.Seats)
            .InclusiveBetween(1, 50)
            .WithMessage("Можно забронировать от 1 до 50 мест.");
    }
}
