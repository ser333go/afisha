# 02. Сервер: DTO, валидация, пароли, JWT и файлы

Здесь: контракты (`Contracts`), FluentValidation, PBKDF2, выпуск JWT и `PosterStorage`.

---

## `Contracts/*.cs` — DTO-записи

Наружу отдаются только DTO, а не сущности EF. Это защищает от утечки лишних полей
(например, `PasswordHash`) и позволяет менять БД независимо от API.

### `Contracts/VenueDto.cs`

```csharp
namespace Afisha.Api.Contracts;                    // 1

public sealed record VenueDto(int Id, string Name, string Address, string City); // 3 — площадка для ответа API
```

`record` — неизменяемая запись: компилятор сам создаёт конструктор, свойства
`init`-only, `Equals`, `GetHashCode`, `ToString`. Для DTO это идеально.

### `Contracts/EventDtos.cs`

```csharp
namespace Afisha.Api.Contracts;

public sealed record EventListItemDto(             // 3 — короткая карточка для списка
    int Id,                                        // 4 — идентификатор
    string Title,                                  // 5 — название
    DateTime StartsAt,                             // 6 — дата начала (UTC, сериализуется с Z)
    decimal Price,                                 // 7 — цена
    int Seats,                                     // 8 — мест
    VenueDto Venue,                                // 9 — вложенный справочник площадки
    string? PosterUrl,                             // 10 — ссылка на постер или null
    int AuthorId);                                 // 11 — id автора (клиент сравнивает со своим)

public sealed record EventDetailsDto(              // 13 — полная карточка
    int Id, string Title, string? Description,     // 14–16
    DateTime StartsAt, decimal Price, int Seats,   // 17–19
    VenueDto Venue, string? PosterUrl,             // 20–21
    int AuthorId, string AuthorName,               // 22–23 — имя автора для отображения
    DateTime CreatedAt);                           // 24

/// <summary>Тело запроса для создания и редактирования мероприятия.</summary>
public sealed record EventRequest(                 // 27 — один DTO и для POST, и для PUT
    string Title,                                  // 28
    string? Description,                           // 29 — описание необязательно
    DateTime StartsAt,                             // 30
    decimal Price,                                 // 31
    int Seats,                                     // 32
    int VenueId);                                  // 33

/// <summary>Параметры списка: страницы, фильтры и сортировка.</summary>
public sealed record EventQuery(                   // 36 — собирается из query-параметров
    int Page = 1,                                  // 37 — страница по умолчанию 1
    int PageSize = 6,                              // 38 — размер страницы по умолчанию 6
    int? VenueId = null,                           // 39 — фильтр по площадке (не задан = все)
    bool? Free = null,                             // 40 — true: только бесплатные, false: только платные
    DateOnly? Date = null,                         // 41 — фильтр по календарному дню
    string Sort = "date",                          // 42 — поле сортировки
    string Order = "asc");                         // 43 — направление
```

Почему `AuthorId` есть в DTO: клиент показывает кнопки «Редактировать/Удалить»
только если `currentUser.id === event.authorId`. Но окончательно права проверяет сервер.

### `Contracts/PagedResult.cs`

```csharp
namespace Afisha.Api.Contracts;

public sealed record PagedResult<T>(               // 3 — универсальная страница
    IReadOnlyList<T> Items,                        // 4 — элементы текущей страницы
    int Page,                                      // 5 — номер страницы
    int PageSize,                                  // 6 — сколько на странице
    int Total)                                     // 7 — всего записей
{
    public int TotalPages =>                       // 9 — вычисляемое свойство (попадает в JSON как totalPages)
        PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize); // 10 — округление вверх, защита от деления на 0
}

public sealed record PosterUploadedDto(string PosterUrl); // 13 — ответ на загрузку файла
```

### `Contracts/AuthDtos.cs`

```csharp
namespace Afisha.Api.Contracts;

public sealed record RegisterRequest(string Email, string DisplayName, string Password); // 3
public sealed record LoginRequest(string Email, string Password);                       // 5
public sealed record UserDto(int Id, string Email, string DisplayName);                // 7 — без хеша пароля!
public sealed record AuthResponse(string Token, UserDto User);                          // 9 — токен + пользователь
```

`UserDto` намеренно **не содержит** `PasswordHash`. Это ответ на вопрос
«почему наружу не отдаём сущности БД».

---

## `Validation/ValidationExtensions.cs` — ошибки → ProblemDetails

```csharp
using System.Text.Json;                    // 1 — JsonNamingPolicy.CamelCase
using FluentValidation.Results;            // 2 — ValidationResult

namespace Afisha.Api.Validation;

public static class ValidationExtensions
{
    /// <summary>Переводит ошибки FluentValidation в словарь errors (ключи — camelCase).</summary>
    public static Dictionary<string, string[]> ToErrorDictionary(this ValidationResult result) // 9 — extension-метод
    {
        return result.Errors                                       // 11 — список ValidationFailure
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName)) // 12 — "Title" → "title"
            .ToDictionary(                                         // 13 — строим словарь
                group => group.Key,                                // 14 — имя поля
                group => group.Select(error => error.ErrorMessage) // 15 — все сообщения поля
                              .Distinct().ToArray());              // 16 — без дублей, в массив
    }
}
```

`camelCase` нужен, чтобы ключи ошибок совпадали с именами контролов Angular-формы
(`title`, `startsAt`, `venueId`). Иначе клиенту пришлось бы переводить `Title` → `title`.

---

## `Validation/ValidationFilter.cs` — единая проверка тела запроса

```csharp
using FluentValidation;

namespace Afisha.Api.Validation;

/// <summary>Endpoint-фильтр: валидирует тело и возвращает 400 с объектом errors.</summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter // 8 — фильтр для любого T
    where T : class                                                              // 9 — только ссылочные типы (DTO)
{
    public async ValueTask<object?> InvokeAsync(                                 // 11 — вызывается до обработчика endpoint'а
        EndpointFilterInvocationContext context,                                 // 12 — аргументы вызова
        EndpointFilterDelegate next)                                             // 13 — «следующий шаг» (обработчик)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();              // 15 — находим DTO среди аргументов
        if (model is null)                                                       // 16 — если DTO нет (например, GET)
        {
            return await next(context);                                          // 18 — просто идём дальше
        }

        var result = await validator.ValidateAsync(model, context.HttpContext.RequestAborted); // 21 — валидируем; отмена запроса
        if (!result.IsValid)                                                     // 22 — есть ошибки?
        {
            return Results.ValidationProblem(result.ToErrorDictionary());        // 24 — 400 + ProblemDetails + errors
        }

        return await next(context);                                              // 27 — ошибок нет — вызываем обработчик
    }
}
```

`IEndpointFilter` — механизм Minimal API: можно вклиниться между маршрутом и
обработчиком. `next` — это либо следующий фильтр, либо сам endpoint.

---

## `Validation/EventRequestValidator.cs` — правила мероприятия

```csharp
using Afisha.Api.Contracts;
using Afisha.Api.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Afisha.Api.Validation;

public sealed class EventRequestValidator : AbstractValidator<EventRequest> // 8
{
    public EventRequestValidator(AppDbContext db)                           // 10 — DbContext нужен для проверки VenueId
    {
        RuleFor(x => x.Title)                                               // 12 — правило для Title
            .Cascade(CascadeMode.Stop)                                       // 13 — при первой ошибке остальные проверки поля не выполняем
            .NotEmpty().WithMessage("Название обязательно.")                  // 14 — не пустое
            .Must(title => !string.IsNullOrWhiteSpace(title))                 // 15 — не из одних пробелов
            .WithMessage("Название не может состоять только из пробелов.")    // 16
            .Length(3, 200).WithMessage("Название должно содержать от 3 до 200 символов."); // 17 — длина

        RuleFor(x => x.Description)                                         // 19
            .MaximumLength(2000).WithMessage("Описание не должно превышать 2000 символов."); // 20

        RuleFor(x => x.StartsAt)                                            // 22
            .Must(startsAt => startsAt > DateTime.UtcNow)                    // 23 — строго в будущем
            .WithMessage("Дата начала должна быть в будущем.");               // 24

        RuleFor(x => x.Price)                                               // 26
            .GreaterThanOrEqualTo(0).WithMessage("Цена не может быть отрицательной.") // 27
            .LessThanOrEqualTo(1_000_000).WithMessage("Цена не должна превышать 1 000 000."); // 28

        RuleFor(x => x.Seats)                                               // 30
            .InclusiveBetween(1, 50_000).WithMessage("Количество мест должно быть от 1 до 50 000."); // 31

        RuleFor(x => x.VenueId)                                             // 33
            .GreaterThan(0).WithMessage("Выберите площадку.")                  // 34
            .MustAsync((venueId, cancellation) =>                            // 35 — асинхронная проверка в БД
                db.Venues.AnyAsync(v => v.Id == venueId, cancellation))      //      существует ли такая площадка
            .WithMessage("Площадка не найдена.");                            // 36
    }
}
```

`WithMessage` задаёт русский текст, который попадёт в `errors`. `MustAsync`
получает `CancellationToken` — если клиент отключился, запрос к БД отменится.

---

## `Validation/EventQueryValidator.cs` — проверка параметров списка

```csharp
using Afisha.Api.Contracts;
using FluentValidation;

namespace Afisha.Api.Validation;

public sealed class EventQueryValidator : AbstractValidator<EventQuery>
{
    private static readonly string[] Sorts = ["date", "price"];   // 9 — допустимые поля сортировки
    private static readonly string[] Orders = ["asc", "desc"];    // 10 — допустимые направления

    public EventQueryValidator()
    {
        RuleFor(x => x.Page)                                      // 14
            .GreaterThanOrEqualTo(1).WithMessage("Номер страницы должен быть не меньше 1."); // 15

        RuleFor(x => x.PageSize)                                  // 17
            .InclusiveBetween(1, 50).WithMessage("Размер страницы должен быть от 1 до 50."); // 18

        RuleFor(x => x.VenueId)                                   // 20
            .GreaterThan(0).WithMessage("Идентификатор площадки должен быть положительным.") // 21
            .When(x => x.VenueId.HasValue);                       // 22 — проверяем только если фильтр задан

        RuleFor(x => x.Sort)                                      // 24
            .Must(sort => Sorts.Contains(sort.ToLowerInvariant())) // 25 — только date/price
            .WithMessage("Сортировка возможна только по date или price."); // 26

        RuleFor(x => x.Order)                                     // 28
            .Must(order => Orders.Contains(order.ToLowerInvariant())) // 29 — только asc/desc
            .WithMessage("Направление сортировки: asc или desc.");  // 30
    }
}
```

Если клиент пришлёт `sort=price; DROP TABLE`, валидатор вернёт 400 — в SQL это
значение не попадёт. Дополнительно в endpoint'е используется `switch`, поэтому
произвольная строка и так не станет частью запроса.

---

## `Validation/AuthValidators.cs` — регистрация и вход

```csharp
using Afisha.Api.Contracts;
using FluentValidation;

namespace Afisha.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)                                     // 10
            .NotEmpty().WithMessage("Email обязателен.")           // 11
            .EmailAddress().WithMessage("Введите корректный email.") // 12
            .MaximumLength(254).WithMessage("Email слишком длинный."); // 13

        RuleFor(x => x.DisplayName)                                // 15
            .Cascade(CascadeMode.Stop)                             // 16
            .NotEmpty().WithMessage("Имя обязательно.")             // 17
            .Must(name => !string.IsNullOrWhiteSpace(name))        // 18
            .WithMessage("Имя не может состоять только из пробелов.") // 19
            .Length(2, 100).WithMessage("Имя должно содержать от 2 до 100 символов."); // 20

        RuleFor(x => x.Password)                                   // 22
            .NotEmpty().WithMessage("Пароль обязателен.")           // 23
            .MinimumLength(8).WithMessage("Пароль должен содержать минимум 8 символов.") // 24
            .MaximumLength(72).WithMessage("Пароль не должен превышать 72 символа.")     // 25
            .Must(password => password.Any(char.IsLetter) && password.Any(char.IsDigit)) // 26
            .WithMessage("Пароль должен содержать хотя бы одну букву и одну цифру.");    // 27
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)                                      // 35
            .NotEmpty().WithMessage("Email обязателен.")            // 36
            .EmailAddress().WithMessage("Введите корректный email."); // 37

        RuleFor(x => x.Password)                                   // 39
            .NotEmpty().WithMessage("Пароль обязателен.");           // 40
    }
}
```

Пароль при входе не проверяется на длину/состав: старые пароли могут не проходить
новые правила, а неверный пароль всё равно даст 401.

---

## `Auth/Pbkdf2PasswordHasher.cs` — хеширование пароля

```csharp
using System.Security.Cryptography;

namespace Afisha.Api.Auth;

public interface IPasswordHasher                            // 5 — интерфейс: легко подменить/замокать
{
    string Hash(string password);                           // 7 — получить хеш
    bool Verify(string password, string passwordHash);      // 9 — проверить пароль
}

/// <summary>PBKDF2-HMAC-SHA256: в базе только соль и хеш.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;                 // 15 — 100 тысяч итераций: медленно перебирать
    private const int SaltSize = 16;                        // 16 — 16 байт соли
    private const int KeySize = 32;                         // 17 — 32 байта результата (256 бит)
    private const string Prefix = "PBKDF2";                 // 18 — метка формата

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize); // 22 — криптостойкая случайная соль
        var key = Rfc2898DeriveBytes.Pbkdf2(                 // 23 — PBKDF2-HMAC-SHA256
            password, salt, Iterations, HashAlgorithmName.SHA256, KeySize); // 24
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}"; // 25 — формат хранения
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');                 // 30 — разбиваем строку на 4 части
        if (parts.Length != 4 || parts[0] != Prefix ||       // 31 — проверяем формат…
            !int.TryParse(parts[1], out var iterations))     // 32 — …и число итераций
        {
            return false;                                    // 34 — битый хеш — не пускаем
        }

        var salt = Convert.FromBase64String(parts[2]);       // 37 — соль из хеша
        var expected = Convert.FromBase64String(parts[3]);   // 38 — ожидаемый хеш
        var actual = Rfc2898DeriveBytes.Pbkdf2(              // 39 — считаем хеш от введённого пароля
            password, salt, iterations, HashAlgorithmName.SHA256, expected.Length); // 40
        return CryptographicOperations.FixedTimeEquals(actual, expected); // 41 — сравнение за постоянное время
    }
}
```

Почему `FixedTimeEquals`: обычное `==` сравнивает байты до первого различия и по
времени ответа теоретически позволяет подбирать хеш. `FixedTimeEquals` сравнивает
всегда одинаковое время.

---

## `Auth/JwtTokenService.cs` — выпуск токена

```csharp
using System.IdentityModel.Tokens.Jwt;                     // 1 — JwtSecurityToken, handler
using System.Security.Claims;                              // 2 — Claim
using System.Text;                                         // 3 — Encoding
using Afisha.Api.Models;
using Microsoft.IdentityModel.Tokens;                      // 5 — SigningCredentials, SymmetricSecurityKey

namespace Afisha.Api.Auth;

/// <summary>Выдаёт JWT. Ключ подписи берётся из конфигурации (user-secrets).</summary>
public sealed class JwtTokenService(IConfiguration configuration) // 10 — конфигурация приходит из DI
{
    public (string Token, DateTime ExpiresAt) CreateToken(User user) // 12 — возвращаем токен и срок
    {
        var key = configuration["Jwt:Key"]                 // 14 — секрет из user-secrets
            ?? throw new InvalidOperationException(        // 15 — без ключа работать нельзя
                "Jwt:Key не задан. Выполните: dotnet user-secrets set \"Jwt:Key\" \"...\".");

        var issuer = configuration["Jwt:Issuer"] ?? "Afisha.Api";       // 17 — издатель
        var audience = configuration["Jwt:Audience"] ?? "Afisha.Client"; // 18 — получатель
        var lifetimeHours = configuration.GetValue("Jwt:LifetimeHours", 12); // 19 — срок жизни, по умолчанию 12 ч
        var expiresAt = DateTime.UtcNow.AddHours(lifetimeHours);        // 20 — момент истечения

        var claims = new List<Claim>                       // 22 — что кладём в токен
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),          // 24 — "sub" = id пользователя
            new(JwtRegisteredClaimNames.Email, user.Email),                // 25 — "email"
            new(JwtRegisteredClaimNames.Name, user.DisplayName),           // 26 — "name"
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")), // 27 — уникальный id токена
        };

        var credentials = new SigningCredentials(          // 29 — чем подписываем
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), // 30 — HMAC-ключ из секрета
            SecurityAlgorithms.HmacSha256);                // 31 — алгоритм HS256

        var token = new JwtSecurityToken(                  // 33 — собираем токен
            issuer: issuer,                                // 34
            audience: audience,                            // 35
            claims: claims,                                // 36
            notBefore: DateTime.UtcNow,                    // 37 — действует с сейчас
            expires: expiresAt,                            // 38 — до expiresAt
            signingCredentials: credentials);              // 39

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt); // 41 — сериализуем в строку
    }
}
```

JWT — это три base64-сегмента `header.payload.signature`. Payload **не шифруется**,
его можно прочитать; защищает именно подпись. Поэтому в токен не кладут пароль.

---

## `Auth/ClaimsPrincipalExtensions.cs` — id из токена

```csharp
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Afisha.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Достаёт id пользователя из claim "sub".</summary>
    public static int? GetUserId(this ClaimsPrincipal principal)      // 10 — extension для HttpContext.User
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) // 12 — сначала "sub"
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);       // 13 — запасной вариант
        return int.TryParse(value, out var id) ? id : null;            // 14 — строку → int; null, если не получилось
    }
}
```

Используется в endpoint'ах: `http.User.GetUserId()`. Если токена нет — вернёт `null`,
и endpoint отдаст 401.

---

## `Storage/PosterStorage.cs` — загрузка постера

```csharp
namespace Afisha.Api.Storage;

/// <summary>Сохраняет постеры на диск под именем Guid, проверяет формат/размер.</summary>
public sealed class PosterStorage(IWebHostEnvironment environment) // 6 — окружение даёт путь к wwwroot
{
    public const long MaxFileSize = 5 * 1024 * 1024;               // 8 — лимит 5 МБ

    private static readonly Dictionary<string, string[]> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) // 10 — расширение → MIME
    {
        [".jpg"] = ["image/jpeg"],                                 // 12
        [".jpeg"] = ["image/jpeg"],                                // 13
        [".png"] = ["image/png"],                                  // 14
        [".webp"] = ["image/webp"],                                // 15
    };

    public async Task<(string? Path, string? Error)> SaveAsync(     // 17 — путь или текст ошибки
        IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)                      // 20 — файла нет
        {
            return (null, "Выберите файл постера.");               // 22
        }

        if (file.Length > MaxFileSize)                             // 25 — больше 5 МБ
        {
            return (null, "Файл не должен превышать 5 МБ.");       // 27
        }

        var extension = Path.GetExtension(file.FileName);          // 30 — ".png"
        if (string.IsNullOrEmpty(extension) ||                     // 31 — расширения нет…
            !AllowedTypes.TryGetValue(extension, out var contentTypes)) // 32 — …или оно не разрешено
        {
            return (null, "Допустимые форматы: jpg, png, webp.");  // 34
        }

        if (!contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)) // 37 — MIME совпадает с расширением?
        {
            return (null, "Тип файла не соответствует расширению. Допустимые форматы: jpg, png, webp."); // 39
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())           // 43 — поток загруженного файла
        using (var memory = new MemoryStream())                    // 44 — читаем в память (≤ 5 МБ)
        {
            await stream.CopyToAsync(memory, cancellationToken);   // 46
            bytes = memory.ToArray();                              // 47
        }

        if (!HasValidSignature(bytes, extension))                  // 50 — проверяем «магические байты»
        {
            return (null, "Содержимое файла не является изображением jpg, png или webp."); // 52
        }

        var directory = Path.Combine(WebRootPath, "uploads", "posters"); // 55 — папка хранения
        Directory.CreateDirectory(directory);                      // 56 — создаём, если нет

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}"; // 58 — имя = GUID + расширение
        var physicalPath = Path.Combine(directory, fileName);      // 59 — полный путь
        await File.WriteAllBytesAsync(physicalPath, bytes, cancellationToken); // 60 — запись на диск

        return ($"/uploads/posters/{fileName}", null);             // 62 — относительный URL для клиента
    }

    public void Delete(string? relativePath)                       // 65 — удалить старый постер
    {
        if (string.IsNullOrWhiteSpace(relativePath))               // 67
        {
            return;                                                // 69
        }

        var relative = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar); // 72 — URL → путь ОС
        var root = Path.GetFullPath(WebRootPath);                  // 73 — корень wwwroot
        var fullPath = Path.GetFullPath(Path.Combine(root, relative)); // 74 — итоговый путь

        if (!fullPath.StartsWith(root, StringComparison.Ordinal))   // 76 — защита от выхода за пределы wwwroot
        {
            return;                                                // 78
        }

        if (File.Exists(fullPath))                                 // 81
        {
            File.Delete(fullPath);                                 // 83
        }
    }

    private string WebRootPath => environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"); // 86

    private static bool HasValidSignature(byte[] bytes, string extension) // 88 — проверка сигнатуры
    {
        return extension.ToLowerInvariant() switch                 // 90
        {
            ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF, // 91 — JPEG
            ".png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47, // 92 — PNG
            ".webp" => bytes.Length >= 12                              // 93 — WebP: "RIFF....WEBP"
                       && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                       && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P',
            _ => false,                                            // 95
        };
    }
}
```

Почему проверяем и расширение, и MIME, и сигнатуру: расширение легко переименовать,
`Content-Type` подделывается клиентом, а сигнатура — уже содержимое файла.
Дополнительно `Delete` защищён от path traversal (`../../etc/passwd`).

---

## Что спросить у себя после этого раздела

1. Зачем DTO, если есть сущности?
2. Что делает `IEndpointFilter` и когда он выполняется?
3. Почему `Results.ValidationProblem` — это 400 с `errors`?
4. Зачем соль в PBKDF2 и почему 100 000 итераций?
5. Почему JWT нельзя считать секретным?
6. Чем `sub` отличается от `nameid`?
7. Зачем проверять сигнатуру файла, если есть `Content-Type`?
8. Почему имя файла — GUID, а не исходное имя?
