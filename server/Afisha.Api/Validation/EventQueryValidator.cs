using Afisha.Api.Contracts;
using FluentValidation;

namespace Afisha.Api.Validation;

public sealed class EventQueryValidator : AbstractValidator<EventQuery>
{
    private static readonly string[] Sorts = ["date", "price"];
    private static readonly string[] Orders = ["asc", "desc"];

    public EventQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Номер страницы должен быть не меньше 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Размер страницы должен быть от 1 до 50.");

        RuleFor(x => x.VenueId)
            .GreaterThan(0).WithMessage("Идентификатор площадки должен быть положительным.")
            .When(x => x.VenueId.HasValue);

        RuleFor(x => x.Sort)
            .Must(sort => Sorts.Contains(sort.ToLowerInvariant()))
            .WithMessage("Сортировка возможна только по date или price.");

        RuleFor(x => x.Order)
            .Must(order => Orders.Contains(order.ToLowerInvariant()))
            .WithMessage("Направление сортировки: asc или desc.");
    }
}
