using Afisha.Api.Contracts;
using Afisha.Api.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Afisha.Api.Validation;

public sealed class EventRequestValidator : AbstractValidator<EventRequest>
{
    public EventRequestValidator(AppDbContext db)
    {
        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Название обязательно.")
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Название не может состоять только из пробелов.")
            .Length(3, 200).WithMessage("Название должно содержать от 3 до 200 символов.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Описание не должно превышать 2000 символов.");

        RuleFor(x => x.StartsAt)
            .Must(startsAt => startsAt > DateTime.UtcNow)
            .WithMessage("Дата начала должна быть в будущем.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Цена не может быть отрицательной.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Цена не должна превышать 1 000 000.");

        RuleFor(x => x.Seats)
            .InclusiveBetween(1, 50_000).WithMessage("Количество мест должно быть от 1 до 50 000.");

        RuleFor(x => x.VenueId)
            .GreaterThan(0).WithMessage("Выберите площадку.")
            .MustAsync((venueId, cancellation) => db.Venues.AnyAsync(v => v.Id == venueId, cancellation))
            .WithMessage("Площадка не найдена.");
    }
}
