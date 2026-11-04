using Afisha.Api.Contracts;
using FluentValidation;

namespace Afisha.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен.")
            .EmailAddress().WithMessage("Введите корректный email.")
            .MaximumLength(254).WithMessage("Email слишком длинный.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Имя обязательно.")
            .Length(2, 100).WithMessage("Имя должно содержать от 2 до 100 символов.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.")
            .MinimumLength(8).WithMessage("Пароль должен содержать минимум 8 символов.")
            .MaximumLength(72).WithMessage("Пароль не должен превышать 72 символа.")
            .Must(password => password.Any(char.IsLetter) && password.Any(char.IsDigit))
            .WithMessage("Пароль должен содержать хотя бы одну букву и одну цифру.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен.")
            .EmailAddress().WithMessage("Введите корректный email.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.");
    }
}
