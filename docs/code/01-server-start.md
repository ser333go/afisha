# 01. Сервер: входная точка, модели и база

Разбираем `Program.cs`, модели `Event/Venue/User`, `AppDbContext`, `SeedData` и фабрику
контекста. Номера в комментариях — это номера строк в реальных файлах.

> **Роли:** у пользователя есть поле `Role`; администратор `admin/admin` создаётся при старте, база лежит в `database/afisha.db`. Подробности — в [09-roles.md](09-roles.md).

---

## `Program.cs` — сборка и настройка приложения

```csharp
using System.Text;                                   // 1 — Encoding: переводим строковый ключ JWT в байты
using Afisha.Api.Auth;                               // 2 — PBKDF2-хешер, JwtTokenService, чтение claim'ов
using Afisha.Api.Contracts;                          // 3 — DTO-записи (EventRequest, LoginRequest и т.д.)
using Afisha.Api.Data;                               // 4 — AppDbContext
using Afisha.Api.Endpoints;                          // 5 — методы MapAuthEndpoints/MapVenueEndpoints/MapEventEndpoints
using Afisha.Api.Storage;                            // 6 — PosterStorage (сохранение файла постера)
using Afisha.Api.Validation;                         // 7 — валидаторы и ValidationFilter
using FluentValidation;                               // 8 — интерфейс IValidator<T>
using Microsoft.AspNetCore.Authentication.JwtBearer;  // 9 — AddJwtBearer: проверка JWT
using Microsoft.EntityFrameworkCore;                  // 10 — UseSqlite и Database.Migrate()
using Microsoft.IdentityModel.Tokens;                 // 11 — SymmetricSecurityKey, TokenValidationParameters
using Scalar.AspNetCore;                              // 12 — UI Scalar для OpenAPI

var builder = WebApplication.CreateBuilder(args);     // 14 — создаёт builder: конфигурация, логирование, DI, окружение
```

`WebApplication.CreateBuilder(args)` читает `appsettings.json`, `appsettings.Development.json`,
переменные окружения (`Jwt__Key`) и user-secrets (в Development). Всё это попадает
в `builder.Configuration` и `builder.Services`.

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>        // 16 — регистрируем DbContext в DI-контейнере
    options.UseSqlite(                                        // 17 — говорим EF Core использовать SQLite;
        builder.Configuration.GetConnectionString("Default")  //      строку подключения берём из appsettings.json,
        ?? "Data Source=afisha.db"));                         //      а если её нет — запасной вариант рядом с приложением
```

`AddDbContext` регистрирует контекст со временем жизни **Scoped**: на каждый HTTP-запрос
создаётся свой `AppDbContext`, после запроса — освобождается. Это важно, потому что
`DbContext` не потокобезопасен.

```csharp
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>(); // 19 — хешер пароля; Scoped — без общего состояния
builder.Services.AddSingleton<JwtTokenService>();                    // 20 — сервис выпуска JWT, зависит только от конфигурации
builder.Services.AddSingleton<PosterStorage>();                      // 21 — работа с файлами, зависит от IWebHostEnvironment

builder.Services.AddScoped<IValidator<EventRequest>, EventRequestValidator>();     // 23 — валидатор создания/правки события
builder.Services.AddScoped<IValidator<EventQuery>, EventQueryValidator>();         // 24 — валидатор page/pageSize/sort
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>(); // 25 — валидатор регистрации
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();     // 26 — валидатор входа

builder.Services.AddProblemDetails();                // 28 — включает ProblemDetails для ошибок и исключений
```

`AddProblemDetails()` нужен, чтобы `UseExceptionHandler()` отдавал не пустой 500,
а JSON вида `{ "title": ..., "status": 500 }`.

```csharp
// Ключ подписи JWT обязан приходить из user-secrets (в git его нет).
var jwtKey = builder.Configuration["Jwt:Key"];                    // 31 — читаем секрет из конфигурации
if (string.IsNullOrWhiteSpace(jwtKey) ||                          // 32 — ключ не пустой…
    Encoding.UTF8.GetByteCount(jwtKey) < 32)                      //      …и не короче 32 байт (требование HMAC-SHA256)
{
    throw new InvalidOperationException(                          // 34 — падаем сразу при старте с понятным текстом
        "Jwt:Key не настроен. Выполните в папке backend/Afisha.Api: " +
        "dotnet user-secrets set \"Jwt:Key\" \"<случайная строка длиной не менее 32 символов>\".");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Afisha.Api";     // 39 — кто выдал токен
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Afisha.Client"; // 40 — для кого токен
```

Ключ **не хранится в коде и не коммитится**: `dotnet user-secrets` кладёт его в файл
вне репозитория (`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`).

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)     // 43 — схема аутентификации по умолчанию — JWT Bearer
    .AddJwtBearer(options =>                                       // 44 — настраиваем проверку токена
    {
        options.MapInboundClaims = false;                          // 46 — не переименовывать claim'ы (оставляем "sub", "email", "name")
        options.TokenValidationParameters = new TokenValidationParameters // 47 — правила проверки
        {
            ValidateIssuer = true,                                 // 49 — проверять поле iss
            ValidIssuer = jwtIssuer,                               // 50 — ожидаемое значение iss
            ValidateAudience = true,                               // 51 — проверять поле aud
            ValidAudience = jwtAudience,                           // 52 — ожидаемое значение aud
            ValidateIssuerSigningKey = true,                       // 53 — проверять подпись
            IssuerSigningKey = new SymmetricSecurityKey(           // 54 — тот же секрет, что использовался при выпуске
                Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,                               // 55 — проверять exp/nbf
            ClockSkew = TimeSpan.FromMinutes(1),                   // 56 — допуск 1 минута на рассинхрон часов
        };

        options.Events = new JwtBearerEvents                       // 59 — свои ответы на 401/403
        {
            OnChallenge = async context =>                         // 61 — срабатывает, когда токена нет/он неверный
            {
                context.HandleResponse();                          // 63 — отключаем стандартный пустой ответ
                context.Response.StatusCode = StatusCodes.Status401Unauthorized; // 64 — код 401
                await Results.Problem(                             // 65 — пишем ProblemDetails вручную
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Требуется вход",
                        detail: "Добавьте заголовок Authorization: Bearer <токен>.")
                    .ExecuteAsync(context.HttpContext);            // 69 — выполняем IResult на текущем HttpContext
            },
            OnForbidden = async context =>                         // 71 — срабатывает на 403 от политики/атрибута
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden; // 73 — код 403
                await Results.Problem(                             // 74 — тоже ProblemDetails
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Действие запрещено",
                        detail: "У вас нет прав на эту операцию.")
                    .ExecuteAsync(context.HttpContext);
            },
        };
    });

builder.Services.AddAuthorization();                           // 83 — включает проверку прав (атрибут/RequireAuthorization)
```

`MapInboundClaims = false` — важная строка: иначе ASP.NET переименует `sub` в длинный
`ClaimTypes.NameIdentifier`. С этой настройкой мы читаем claim `sub` как есть
(см. `ClaimsPrincipalExtensions`).

```csharp
builder.Services.AddCors(options => options.AddPolicy("spa", policy => policy // 85 — политика CORS для Angular
    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")            // 86 — разрешаем dev-сервер Angular
    .AllowAnyHeader()                                                          // 87 — любые заголовки (в т.ч. Authorization)
    .AllowAnyMethod()));                                                       // 88 — GET/POST/PUT/DELETE и т.д.

builder.Services.AddOpenApi();                                 // 90 — генерирует OpenAPI-документ /openapi/v1.json

var app = builder.Build();                                     // 92 — фиксируем конфигурацию и создаём приложение
```

CORS нужен, если клиент обращается к API напрямую (в dev обычно работает прокси
Angular, но политика не мешает).

```csharp
using (var scope = app.Services.CreateScope())                 // 94 — создаём scope: DbContext — scoped-сервис
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); // 96 — достаём контекст
    db.Database.Migrate();                                     // 97 — применяем все миграции (создаёт afisha.db)

    var webRoot = app.Environment.WebRootPath                  // 99 — физический путь к wwwroot
        ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "posters")); // 100 — папка для постеров должна существовать
}
```

`Migrate()` при старте удобен для учебного проекта: не нужно вручную выполнять
`dotnet ef database update`. Если БД нет — она создастся и засеется через `HasData`.

```csharp
app.UseExceptionHandler();                                     // 103 — необработанные исключения → ProblemDetails 500
app.UseStaticFiles();                                          // 104 — отдаём файлы из wwwroot (/uploads/posters/...)
app.UseCors("spa");                                            // 105 — применяем CORS-политику
app.UseAuthentication();                                       // 106 — читаем JWT и создаём HttpContext.User
app.UseAuthorization();                                        // 107 — проверяем RequireAuthorization

app.MapOpenApi();                                              // 109 — /openapi/v1.json
app.MapScalarApiReference(options => options.WithTitle("Afisha API")); // 110 — UI /scalar
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription(); // 111 — корень ведёт на Scalar

app.MapAuthEndpoints();                                        // 113 — /api/auth/*
app.MapVenueEndpoints();                                       // 114 — /api/venues
app.MapEventEndpoints();                                       // 115 — /api/events/*

app.Run();                                                     // 117 — запуск Kestrel

public partial class Program;                                  // 119 — делает Program доступным для тестов/WebApplicationFactory
```

**Порядок middleware важен:** `ExceptionHandler` → `StaticFiles` → `Cors` →
`Authentication` → `Authorization`. Аутентификация обязана идти до авторизации,
иначе `RequireAuthorization()` не увидит пользователя.

---

## `Models/Event.cs` — главная сущность

```csharp
namespace Afisha.Api.Models;                        // 1 — пространство имён слоя моделей

/// <summary>Мероприятие афиши.</summary>
public class Event                                  // 4 — класс-сущность, EF Core сделает из него таблицу Events
{
    public int Id { get; set; }                     // 6 — первичный ключ (EF сам считает Id ключом)

    public string Title { get; set; } = string.Empty; // 8 — название; = string.Empty убирает null при создании

    public string? Description { get; set; }        // 10 — описание необязательно (nullable)

    /// <summary>Дата и время начала, всегда хранится в UTC.</summary>
    public DateTime StartsAt { get; set; }          // 13 — дата начала; на чтении конвертер помечает Kind=Utc

    /// <summary>Цена билета; 0 — бесплатно.</summary>
    public decimal Price { get; set; }              // 16 — цена в сумах; в БД хранится как long (копейки)

    /// <summary>Количество мест.</summary>
    public int Seats { get; set; }                  // 19 — сколько мест

    /// <summary>Относительный путь к загруженному постеру.</summary>
    public string? PosterPath { get; set; }         // 22 — например /uploads/posters/{guid}.png; может быть null

    public int VenueId { get; set; }                // 24 — внешний ключ на Venues

    public Venue Venue { get; set; } = null!;       // 26 — навигационное свойство; null! говорит компилятору «EF заполнит»

    /// <summary>Автор записи — только он может менять и удалять мероприятие.</summary>
    public int AuthorId { get; set; }               // 29 — внешний ключ на Users

    public User Author { get; set; } = null!;       // 31 — навигация на автора

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 33 — когда создана запись
}
```

`decimal Price` в SQLite не поддерживается нативно — поэтому в `AppDbContext`
настроен конвертер в `long` (копейки). Иначе сортировка по цене сравнивала бы строки
(«100» < «90»).

---

## `Models/Venue.cs` — справочник площадок

```csharp
namespace Afisha.Api.Models;

/// <summary>Площадка — справочник, с которым связана главная сущность.</summary>
public class Venue
{
    public int Id { get; set; }                     // 8 — ключ
    public string Name { get; set; } = string.Empty;   // 10 — название площадки
    public string Address { get; set; } = string.Empty; // 12 — адрес
    public string City { get; set; } = string.Empty;    // 14 — город

    public ICollection<Event> Events { get; set; } = new List<Event>(); // 16 — обратная навигация: у площадки много событий
}
```

`ICollection<Event> Events` — сторона «один» связи «один-ко-многим». EF Core
заполняет её при `Include`, если понадобится.

---

## `Models/User.cs` — пользователь

```csharp
namespace Afisha.Api.Models;

public class User
{
    public int Id { get; set; }                                  // 7 — ключ
    public string Email { get; set; } = string.Empty;            // 9 — email (уникальный индекс в БД)
    public string DisplayName { get; set; } = string.Empty;      // 11 — имя для отображения

    /// <summary>PBKDF2-хеш пароля. Открытый пароль не хранится нигде.</summary>
    public string PasswordHash { get; set; } = string.Empty;     // 14 — строка вида PBKDF2$100000$salt$hash

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;   // 16 — дата регистрации

    public ICollection<Event> Events { get; set; } = new List<Event>(); // 18 — события, созданные пользователем
}
```

---

## `Data/AppDbContext.cs` — конфигурация EF Core

```csharp
using Afisha.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion; // 3 — ValueConverter<>

namespace Afisha.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) // 7 — primary constructor: options приходят из DI
{
    public DbSet<Event> Events => Set<Event>();      // 9 — таблица Events, доступ через LINQ
    public DbSet<Venue> Venues => Set<Venue>();      // 11 — таблица Venues
    public DbSet<User> Users => Set<User>();         // 13 — таблица Users

    protected override void OnModelCreating(ModelBuilder modelBuilder) // 15 — здесь описываем схему
    {
        base.OnModelCreating(modelBuilder);          // 17 — не теряем стандартные настройки EF

        modelBuilder.Entity<Venue>(venue =>          // 19 — настройка Venue
        {
            venue.ToTable("Venues");                 // 21 — имя таблицы
            venue.Property(v => v.Name).IsRequired().HasMaxLength(120);    // 22 — NOT NULL, максимум 120
            venue.Property(v => v.Address).IsRequired().HasMaxLength(200); // 23 — адрес обязателен
            venue.Property(v => v.City).IsRequired().HasMaxLength(80);     // 24 — город обязателен
        });

        modelBuilder.Entity<User>(user =>            // 27 — настройка User
        {
            user.ToTable("Users");                   // 29
            user.Property(u => u.Email).IsRequired().HasMaxLength(254); // 30 — email обязателен
            user.HasIndex(u => u.Email).IsUnique();  // 31 — уникальный индекс: два пользователя с одним email невозможны
            user.Property(u => u.DisplayName).IsRequired().HasMaxLength(100); // 32
            user.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500); // 33
        });

        modelBuilder.Entity<Event>(entity =>         // 36 — настройка Event
        {
            entity.ToTable("Events");                // 38
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200); // 39
            entity.Property(e => e.Description).HasMaxLength(2000);       // 40 — описание может быть null

            // SQLite не умеет decimal: храним цену в целых копейках, чтобы сортировка
            // по цене выполнялась базой корректно (целочисленное сравнение, а не строк).
            entity.Property(e => e.Price).HasConversion<long>(            // 44 — конвертер decimal <-> long
                rubles => (long)Math.Round(rubles * 100m, MidpointRounding.AwayFromZero), // 45 — при записи: сумы → копейки
                cents => cents / 100m);                                   // 46 — при чтении: копейки → сумы

            // SQLite не хранит DateTimeKind, поэтому при чтении явно помечаем время как UTC.
            entity.Property(e => e.StartsAt).HasConversion(               // 49 — конвертер DateTime <-> DateTime
                new ValueConverter<DateTime, DateTime>(
                    utc => utc,                                           // 51 — при записи значение не меняем
                    stored => DateTime.SpecifyKind(stored, DateTimeKind.Utc))); // 52 — при чтении ставим Kind=Utc

            entity.HasOne(e => e.Venue)              // 54 — у события одна площадка
                .WithMany(v => v.Events)             // 55 — у площадки много событий
                .HasForeignKey(e => e.VenueId)       // 56 — внешний ключ VenueId
                .OnDelete(DeleteBehavior.Restrict);  // 57 — нельзя удалить площадку, пока на неё ссылаются события

            entity.HasOne(e => e.Author)             // 59 — у события один автор
                .WithMany(u => u.Events)             // 60 — у пользователя много событий
                .HasForeignKey(e => e.AuthorId)      // 61 — внешний ключ AuthorId
                .OnDelete(DeleteBehavior.Restrict);  // 62 — нельзя удалить пользователя с событиями

            entity.HasIndex(e => e.StartsAt);        // 64 — индекс для сортировки/фильтра по дате
            entity.HasIndex(e => e.Price);           // 65 — индекс для сортировки по цене
            entity.HasIndex(e => e.VenueId);         // 66 — индекс для фильтра по площадке
            entity.HasIndex(e => e.CreatedAt);       // 67 — индекс из второй миграции
        });

        modelBuilder.Entity<Venue>().HasData(SeedData.Venues); // 70 — начальные площадки попадут в миграцию
        modelBuilder.Entity<User>().HasData(SeedData.Users);   // 71 — начальные пользователи
        modelBuilder.Entity<Event>().HasData(SeedData.Events); // 72 — начальные события
    }
}
```

Что важно на защите:

- `HasConversion<long>` — почему цена не `decimal` в БД (SQLite).
- `ValueConverter` для `StartsAt` — почему клиент получает дату с `Z` (UTC).
- `DeleteBehavior.Restrict` — защита справочника от удаления «в никуда».
- `HasData` — данные попадают в миграцию, а не создаются вручную.

---

## `Data/AppDbContextFactory.cs` — контекст для команд EF

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;   // 2 — IDesignTimeDbContextFactory

namespace Afisha.Api.Data;

/// <summary>Нужен только для команд `dotnet ef migrations ...`.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext> // 7
{
    public AppDbContext CreateDbContext(string[] args) // 9 — EF вызывает этот метод при генерации миграций
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=afisha.db")        // 13 — строка подключения не важна: схема строится из модели
            .Options;

        return new AppDbContext(options);              // 16 — отдаём контекст инструменту
    }
}
```

Зачем: `dotnet ef` не запускает всё приложение (а оно требует `Jwt:Key`), а создаёт
контекст напрямую.

---

## `Data/SeedData.cs` — начальные данные

```csharp
using Afisha.Api.Models;

namespace Afisha.Api.Data;

/// <summary>Начальные данные, которые попадают в базу через HasData.</summary>
public static class SeedData
{
    private static readonly DateTime SeededAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc); // 10 — общая дата создания записей
```

`SeededAt` — фиксированная дата. `HasData` требует **константных** значений, иначе
миграция при каждом запуске считалась бы новой. Поэтому здесь нельзя `DateTime.UtcNow`.

```csharp
    public static readonly Venue[] Venues =       // 12 — массив площадок
    [
        new() { Id = 1, Name = "Дом культуры «Галактика»", Address = "ул. Амира Темура, 12", City = "Ташкент" }, // 14
        new() { Id = 2, Name = "Клуб «Фонарь»", Address = "ул. Шота Руставели, 8", City = "Ташкент" },          // 15
        new() { Id = 3, Name = "Театр «Ильхом»", Address = "ул. Пахтакорская, 5", City = "Ташкент" },           // 16
        new() { Id = 4, Name = "Выставочный зал «Хумо»", Address = "пр. Мустакиллик, 3", City = "Ташкент" },   // 17
        new() { Id = 5, Name = "Парк «Анхор»", Address = "ул. Анхор, 1", City = "Ташкент" },                    // 18
    ];
```

Id заданы вручную — это обязательно для `HasData`: по ним EF понимает, какие строки
вставлять, а какие обновлять.

```csharp
    public static readonly User[] Users =         // 20
    [
        new()
        {
            Id = 1,                                                   // 23
            Email = "demo@afisha.local",                              // 24
            DisplayName = "Демо-автор",                               // 25
            PasswordHash = "PBKDF2$100000$+Zvdd1T8NY9QwmJAoNz1bA==$+Izq8RKzDap0a1JJGgDdIPivL830Ot9ICLw35Wt8h0k=", // 26
            CreatedAt = SeededAt,                                     // 27
        },
        new()
        {
            Id = 2,                                                   // 31
            Email = "guest@afisha.local",                             // 32
            DisplayName = "Второй автор",                             // 33
            PasswordHash = "PBKDF2$100000$XHQguj2i9735t7U1mfcnkg==$7sMNtwXrbhgoOOzuKURdTPrDAIqWgx2twW/o0eo2H3k=", // 34
            CreatedAt = SeededAt,                                     // 35
        },
    ];
```

Формат хеша: `PBKDF2$<итерации>$<соль base64>$<хеш base64>`. Пароли:
`Demo12345` и `Guest12345`. В репозитории только хеши — восстановить пароль нельзя.

```csharp
    public static readonly Event[] Events =       // 39
    [
        new()
        {
            Id = 1,                                                   // 42
            Title = "Ночь космоса",                                   // 43
            Description = "Музыкально-визуальное шоу под куполом: ...", // 44
            StartsAt = new DateTime(2027, 2, 14, 13, 0, 0, DateTimeKind.Utc), // 45 — 18:00 по Ташкенту
            Price = 120_000m,                                         // 46 — 120 000 сум
            Seats = 200,                                              // 47
            VenueId = 1,                                              // 48 — ДК «Галактика»
            AuthorId = 1,                                             // 49 — demo@afisha.local
            CreatedAt = SeededAt,                                     // 50
        },
        // ... ещё 7 событий с той же структурой
    ];
```

Остальные семь событий отличаются только значениями:

| Id | Название | UTC-время | Цена | Мест | VenueId | AuthorId |
| --- | --- | --- | --- | --- | --- | --- |
| 2 | Джаз на крыше | 2027-03-06 14:00 | 180 000 | 80 | 2 | 1 |
| 3 | Ильхом: «Тартюф» | 2027-03-21 10:00 | 90 000 | 120 | 3 | 2 |
| 4 | Выставка современного искусства | 2027-04-02 04:00 | 0 | 500 | 4 | 1 |
| 5 | Open-air кино в «Анхоре» | 2027-04-18 14:00 | 0 | 300 | 5 | 2 |
| 6 | Симфонический вечер | 2027-05-09 11:00 | 250 000 | 400 | 1 | 1 |
| 7 | Стендап: открытый микрофон | 2027-05-23 13:00 | 60 000 | 60 | 2 | 2 |
| 8 | Детский спектакль «Айболит» | 2027-06-01 05:00 | 45 000 | 150 | 3 | 1 |

Почему даты в 2027 году: события должны проходить серверную проверку «дата в будущем»
и не блокироваться правилом 24 часов во время демонстрации.

---

## Что спросить у себя после этого раздела

1. Почему `DbContext` регистрируется как `Scoped`, а `JwtTokenService` — как `Singleton`?
2. Зачем `MapInboundClaims = false`?
3. Почему цена хранится в копейках, а не как `decimal`?
4. Что делает `Database.Migrate()` при старте?
5. Почему в `HasData` нельзя использовать `DateTime.UtcNow`?
6. Чем `HasData` отличается от обычного `INSERT` в коде?
