using System.Text.Json;
using FluentValidation.Results;

namespace Afisha.Api.Validation;

public static class ValidationExtensions
{
    /// <summary>Переводит ошибки FluentValidation в словарь errors для ProblemDetails (ключи — camelCase).</summary>
    public static Dictionary<string, string[]> ToErrorDictionary(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
    }
}
