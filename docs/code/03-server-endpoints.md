# 03. Сервер: endpoints и миграции

Здесь три файла endpoint'ов и объяснение миграций. Это самая «показываемая» часть
на защите: именно тут видно коды 200/201/204/400/401/403/404.

---

## `Endpoints/AuthEndpoints.cs` — регистрация и вход

```csharp
using System.IdentityModel.Tokens.Jwt;                    // 1 — имена claim'ов
using Afisha.Api.Auth;                                    // 2 — хешер, JWT, GetUserId
using Afisha.Api.Contracts;                               // 3 — DTO
using Afisha.Api.Data;                                    // 4 — DbContext
using Afisha.Api.Models;                                  // 5 — User
using Afisha.Api.Validation;                              // 6 — ValidationFilter
using Microsoft.EntityFrameworkCore;                      // 7 — AnyAsync/FirstOrDefaultAsync

namespace Afisha.Api.Endpoints;

public static class AuthEndpoints                               // 11 — статический класс: endpoint'ы без состояния
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app) // 13 — extension для Program.cs
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth"); // 15 — все маршруты начинаются с /api/auth

        group.MapPost("/register", Register)                    // 17 — POST /api/auth/register
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>() // 18 — сначала валидация DTO
            .WithName("Register")                               // 19 — имя операции для OpenAPI
            .WithSummary("Регистрация")                         // 20 — описание
            .Produces<AuthResponse>(StatusCodes.Status201Created) // 21 — документируем 201
            .ProducesValidationProblem();                       // 22 — документируем 400 с errors

        group.MapPost("/login", Login)                          // 24 — POST /api/auth/login
            .AddEndpointFilter<ValidationFilter<LoginRequest>>() // 25
            .WithName("Login")
            .WithSummary("Вход, выдаёт JWT")
            .Produces<AuthResponse>()                           // 28 — 200
            .ProducesProblem(StatusCodes.Status401Unauthorized) // 29 — неверный пароль
            .ProducesValidationProblem();                       // 30 — 400

        group.MapGet("/me", Me)                                 // 32 — GET /api/auth/me
            .RequireAuthorization()                             // 33 — только с токеном
            .WithName("Me")
            .WithSummary("Текущий пользователь")
            .Produces<UserDto>()                                // 36 — 200
            .ProducesProblem(StatusCodes.Status401Unauthorized); // 37

        return group;                                           // 39
    }

    private static async Task<IResult> Register(                // 41 — обработчик регистрации
        RegisterRequest request,                                // 42 — тело запроса (уже провалидировано)
        AppDbContext db,                                        // 43 — БД
        IPasswordHasher passwordHasher,                         // 44 — хеширование
        JwtTokenService jwt,                                    // 45 — выпуск токена
        CancellationToken cancellationToken)                    // 46 — отмена запроса
    {
        var email = request.Email.Trim().ToLowerInvariant();   // 48 — email без пробелов и в нижнем регистре

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken)) // 50 — уже есть такой?
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> // 52 — 400 в формате ProblemDetails
            {
                ["email"] = ["Пользователь с таким email уже зарегистрирован."], // 54 — ошибка под полем email
            });
        }

        var user = new User                                    // 57 — создаём сущность
        {
            Email = email,                                     // 58
            DisplayName = request.DisplayName.Trim(),          // 59 — имя без пробелов по краям
            PasswordHash = passwordHasher.Hash(request.Password), // 60 — в БД только хеш
            CreatedAt = DateTime.UtcNow,                       // 61
        };

        db.Users.Add(user);                                    // 63 — готовим INSERT
        await db.SaveChangesAsync(cancellationToken);          // 64 — выполняем; Id становится известен

        var (token, _) = jwt.CreateToken(user);                // 66 — сразу логиним: выдаём JWT
        var response = new AuthResponse(token, new UserDto(user.Id, user.Email, user.DisplayName)); // 67
        return Results.Created("/api/auth/me", response);      // 68 — 201 + Location + тело
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        AppDbContext db,
        IPasswordHasher passwordHasher,
        JwtTokenService jwt,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();   // 77 — нормализуем email
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken); // 78 — ищем пользователя

        if (user is null ||                                    // 80 — нет пользователя…
            !passwordHasher.Verify(request.Password, user.PasswordHash)) // 81 — …или пароль не подошёл
        {
            return Results.Problem(                            // 83 — 401 ProblemDetails
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Неверный email или пароль",
                detail: "Проверьте введённые данные и попробуйте снова.");
        }

        var (token, _) = jwt.CreateToken(user);                // 88 — выпускаем токен
        return Results.Ok(new AuthResponse(token, new UserDto(user.Id, user.Email, user.DisplayName))); // 89 — 200
    }

    private static async Task<IResult> Me(
        HttpContext http,                                      // 93 — тут лежит User из токена
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();                    // 97 — id из claim "sub"
        if (userId is null)                                    // 98
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход"); // 100
        }

        var user = await db.Users.AsNoTracking()               // 103 — только чтение, без отслеживания
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken); // 104
        if (user is null)                                      // 105 — токен есть, а пользователя удалили
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Пользователь не найден"); // 107
        }

        return Results.Ok(new UserDto(user.Id, user.Email, user.DisplayName)); // 110 — 200
    }
}
```

Обратите внимание: **пароль нигде не логируется и не возвращается**. Наружу уходит
только `UserDto`.

---

## `Endpoints/VenueEndpoints.cs` — справочник

```csharp
using Afisha.Api.Contracts;
using Afisha.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Afisha.Api.Endpoints;

public static class VenueEndpoints
{
    public static RouteGroupBuilder MapVenueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/venues").WithTags("Venues"); // 12 — /api/venues

        group.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) => // 14 — GET /api/venues
        {
            var venues = await db.Venues                    // 16 — запрос к таблице Venues
                .AsNoTracking()                             // 17 — без отслеживания изменений (только чтение)
                .OrderBy(v => v.Name)                       // 18 — по алфавиту
                .Select(v => new VenueDto(v.Id, v.Name, v.Address, v.City)) // 19 — сразу в DTO
                .ToListAsync(cancellationToken);            // 20 — выполняем SQL

            return Results.Ok(venues);                      // 22 — 200 + массив
        })
        .WithName("GetVenues")
        .WithSummary("Справочник площадок")
        .Produces<List<VenueDto>>();                        // 26 — документируем 200

        return group;
    }
}
```

Справочник доступен без токена: он нужен и гостю, и форме создания.

---

## `Endpoints/EventEndpoints.cs` — CRUD, фильтры, файл

### Регистрация маршрутов

```csharp
using Afisha.Api.Auth;                 // 1 — GetUserId
using Afisha.Api.Contracts;            // 2 — DTO
using Afisha.Api.Data;                 // 3 — DbContext
using Afisha.Api.Models;               // 4 — Event
using Afisha.Api.Storage;              // 5 — PosterStorage
using Afisha.Api.Validation;           // 6 — ValidationFilter
using FluentValidation;                // 7 — IValidator
using Microsoft.EntityFrameworkCore;   // 8 — Include/ToListAsync

namespace Afisha.Api.Endpoints;

public static class EventEndpoints
{
    private static readonly TimeSpan EditLockWindow = TimeSpan.FromHours(24); // 14 — правило «меньше 24 часов»

    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events"); // 18 — префикс /api/events

        group.MapGet("/", GetEvents)                               // 20 — GET список
            .WithName("GetEvents")
            .WithSummary("Список мероприятий: страницы, фильтры, сортировка")
            .Produces<PagedResult<EventListItemDto>>()             // 23 — 200
            .ProducesValidationProblem();                          // 24 — 400 (pageSize=0 и т.п.)

        group.MapGet("/{id:int}", GetEventById)                    // 26 — GET по id; :int — ограничение типа
            .WithName("GetEventById")
            .WithSummary("Мероприятие по id")
            .Produces<EventDetailsDto>()                           // 29 — 200
            .ProducesProblem(StatusCodes.Status404NotFound);       // 30 — 404

        group.MapPost("/", CreateEvent)                            // 32 — POST создание
            .RequireAuthorization()                                // 33 — только вошедший (иначе 401)
            .AddEndpointFilter<ValidationFilter<EventRequest>>()    // 34 — валидация тела
            .WithName("CreateEvent")
            .WithSummary("Создать мероприятие (только для вошедших)")
            .Produces<EventDetailsDto>(StatusCodes.Status201Created) // 37 — 201
            .ProducesValidationProblem()                           // 38 — 400
            .ProducesProblem(StatusCodes.Status401Unauthorized);   // 39 — 401

        group.MapPut("/{id:int}", UpdateEvent)                     // 41 — PUT
            .RequireAuthorization()                                // 42
            .AddEndpointFilter<ValidationFilter<EventRequest>>()    // 43
            .WithName("UpdateEvent")
            .WithSummary("Изменить мероприятие (только автор, не позднее чем за 24 часа)")
            .Produces<EventDetailsDto>()                           // 46 — 200
            .ProducesValidationProblem()                           // 47
            .ProducesProblem(StatusCodes.Status401Unauthorized)    // 48
            .ProducesProblem(StatusCodes.Status403Forbidden)       // 49 — не автор
            .ProducesProblem(StatusCodes.Status404NotFound);       // 50 — нет такой записи

        group.MapDelete("/{id:int}", DeleteEvent)                  // 52
            .RequireAuthorization()
            .WithName("DeleteEvent")
            .WithSummary("Удалить мероприятие (только автор, не позднее чем за 24 часа)")
            .Produces(StatusCodes.Status204NoContent)              // 56 — 204 без тела
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:int}/poster", UploadPoster)            // 61 — загрузка файла
            .RequireAuthorization()
            .DisableAntiforgery()                                  // 63 — отключаем antiforgery для multipart
            .WithName("UploadPoster")
            .WithSummary("Загрузить постер (только автор, jpg/png/webp до 5 МБ)")
            .Accepts<IFormFile>("multipart/form-data")             // 66 — OpenAPI: файл
            .Produces<PosterUploadedDto>()                         // 67 — 200
            .ProducesValidationProblem()                           // 68 — 400 (размер/формат)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
```

`RequireAuthorization()` — это и есть 401 для гостя. `AddEndpointFilter` стоит **после**
`RequireAuthorization`, но фактически сначала выполняется авторизация, потом фильтр
валидации, потом обработчик.

### `GET /api/events` — список

```csharp
    private static async Task<IResult> GetEvents(
        AppDbContext db, IValidator<EventQuery> validator, CancellationToken cancellationToken, // 77–79 — сервисы
        int page = 1, int pageSize = 6, int? venueId = null, bool? free = null,               // 80–83 — query-параметры
        DateOnly? date = null, string sort = "date", string order = "asc")                   // 84–86
    {
        var query = new EventQuery(page, pageSize, venueId, free, date, sort, order); // 88 — собираем DTO запроса
        var validation = await validator.ValidateAsync(query, cancellationToken);     // 89 — проверяем page/pageSize/sort
        if (!validation.IsValid)                                                      // 90
        {
            return Results.ValidationProblem(validation.ToErrorDictionary());         // 92 — 400 + errors
        }

        var events = db.Events.AsNoTracking();                                        // 95 — IQueryable, SQL ещё не выполнен

        if (venueId is not null)                                                      // 97 — фильтр по площадке
        {
            events = events.Where(e => e.VenueId == venueId);                         // 99 — добавили условие в SQL
        }

        if (free is true)                                                             // 102 — только бесплатные
        {
            events = events.Where(e => e.Price == 0);                                 // 104
        }
        else if (free is false)                                                       // 106 — только платные
        {
            events = events.Where(e => e.Price > 0);                                  // 108
        }

        if (date is { } day)                                                          // 111 — фильтр по дате
        {
            var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);           // 113 — начало дня UTC
            var to = from.AddDays(1);                                                 // 114 — начало следующего дня
            events = events.Where(e => e.StartsAt >= from && e.StartsAt < to);        // 115 — полуинтервал [from, to)
        }

        var total = await events.CountAsync(cancellationToken);                       // 118 — ВСЕГО записей до пагинации
        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase); // 119 — направление

        var sorted = sort.ToLowerInvariant() switch                                   // 121 — выбор сортировки
        {
            "price" => descending                                                     // 123 — по цене
                ? events.OrderByDescending(e => e.Price).ThenBy(e => e.StartsAt)      // 124 — дорогие → дешёвые, при равенстве — по дате
                : events.OrderBy(e => e.Price).ThenBy(e => e.StartsAt),               // 125 — дешёвые → дорогие
            _ => descending                                                           // 126 — по дате (по умолчанию)
                ? events.OrderByDescending(e => e.StartsAt).ThenBy(e => e.Id)         // 127 — поздние → ранние
                : events.OrderBy(e => e.StartsAt).ThenBy(e => e.Id),                  // 128 — ранние → поздние
        };

        var items = await sorted                                                      // 131 — выполняем запрос
            .Skip((page - 1) * pageSize)                                              // 132 — пропускаем предыдущие страницы
            .Take(pageSize)                                                           // 133 — берём только pageSize
            .Select(e => new EventListItemDto(                                        // 134 — проекция в DTO (в SQL только нужные поля)
                e.Id, e.Title,                                                        // 135–136
                DateTime.SpecifyKind(e.StartsAt, DateTimeKind.Utc),                   // 137 — гарантируем Z в JSON
                e.Price, e.Seats,                                                     // 138–139
                new VenueDto(e.Venue.Id, e.Venue.Name, e.Venue.Address, e.Venue.City),// 140 — join на Venues
                e.PosterPath, e.AuthorId))                                            // 141–142
            .ToListAsync(cancellationToken);                                          // 143

        return Results.Ok(new PagedResult<EventListItemDto>(items, page, pageSize, total)); // 145 — 200 + page/pageSize/total
    }
```

Почему `CountAsync` до `Skip/Take`: `total` нужен для кнопок «Назад/Вперёд» и подписи
«Страница 1 из 2», а `Take` уже обрезал бы данные.

Почему `switch` по строке: в SQL попадает только выбранная ветка, а не пользовательская
строка. Плюс валидатор разрешает только `date`/`price`.

### `GET /api/events/{id}`

```csharp
    private static async Task<IResult> GetEventById(int id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Events                    // 153 — запрос
            .AsNoTracking()                             // 154
            .Include(e => e.Venue)                      // 155 — подгружаем площадку (JOIN)
            .Include(e => e.Author)                     // 156 — подгружаем автора
            .FirstOrDefaultAsync(e => e.Id == id, ct);  // 157 — null, если нет

        return entity is null ? NotFound(id) : Results.Ok(ToDetails(entity)); // 159 — 404 или 200
    }
```

`Include` нужен, потому что `ToDetails` обращается к `entity.Venue.Name` и
`entity.Author.DisplayName`. Без `Include` они были бы `null`.

### `POST /api/events` — создание

```csharp
    private static async Task<IResult> CreateEvent(
        EventRequest request,          // 163 — уже провалидированный DTO
        HttpContext http,              // 164 — чтобы взять User из токена
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = http.User.GetUserId();                       // 168 — id автора из JWT
        if (userId is null)                                       // 169 — теоретически возможно, если фильтр пропустил
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход"); // 171
        }

        var entity = new Event                                    // 174 — новая сущность
        {
            Title = request.Title.Trim(),                         // 176 — обрезаем пробелы
            Description = string.IsNullOrWhiteSpace(request.Description) // 177 — пустое описание → null
                ? null : request.Description.Trim(),
            StartsAt = NormalizeUtc(request.StartsAt),            // 178 — приводим к UTC
            Price = request.Price,                                // 179
            Seats = request.Seats,                                // 180
            VenueId = request.VenueId,                            // 181 — валидатор уже проверил, что площадка есть
            AuthorId = userId.Value,                              // 182 — автор = текущий пользователь (из токена, не из тела!)
            CreatedAt = DateTime.UtcNow,                          // 183
        };

        db.Events.Add(entity);                                    // 186 — готовим INSERT
        await db.SaveChangesAsync(ct);                            // 187 — сохраняем

        var created = await db.Events                             // 189 — перечитываем с навигациями
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Author)
            .FirstAsync(e => e.Id == entity.Id, ct);              // 193

        return Results.Created($"/api/events/{entity.Id}", ToDetails(created)); // 195 — 201 + Location
    }
```

Ключевая строка — `AuthorId = userId.Value`: автор берётся из токена. Если бы мы брали
его из тела, любой мог бы создать запись «от чужого имени».

### `PUT /api/events/{id}` — изменение

```csharp
    private static async Task<IResult> UpdateEvent(
        int id, EventRequest request, HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var userId = http.User.GetUserId();                       // 205
        if (userId is null)                                       // 206
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход"); // 208
        }

        var entity = await db.Events                              // 211 — ищем запись
            .Include(e => e.Venue)
            .Include(e => e.Author)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity is null)                                       // 216 — нет записи → 404
        {
            return NotFound(id);
        }

        if (entity.AuthorId != userId)                            // 221 — не автор → 403
        {
            return Forbidden();
        }

        if (IsLocked(entity))                                     // 226 — меньше 24 часов → 400
        {
            return Locked();
        }

        entity.Title = request.Title.Trim();                      // 231 — обновляем поля
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(); // 232
        entity.StartsAt = NormalizeUtc(request.StartsAt);         // 233
        entity.Price = request.Price;                             // 234
        entity.Seats = request.Seats;                             // 235
        entity.VenueId = request.VenueId;                         // 236

        await db.SaveChangesAsync(ct);                            // 238 — UPDATE

        // Venue мог измениться — перечитываем вместе со справочником.
        var updated = await db.Events                             // 241
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Author)
            .FirstAsync(e => e.Id == id, ct);                     // 245

        return Results.Ok(ToDetails(updated));                    // 247 — 200
    }
```

Порядок проверок важен: **404 → 403 → 400**. Сначала «есть ли запись», потом «твой ли
это автор», потом «не заблокирована ли».

### `DELETE /api/events/{id}`

```csharp
    private static async Task<IResult> DeleteEvent(
        int id, HttpContext http, AppDbContext db, PosterStorage posterStorage, CancellationToken ct)
    {
        var userId = http.User.GetUserId();                       // 257
        if (userId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход"); // 260
        }

        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct); // 263 — без Include: нужен только Id/Author/StartsAt
        if (entity is null)                                       // 264
        {
            return NotFound(id);
        }

        if (entity.AuthorId != userId)                            // 269
        {
            return Forbidden();
        }

        if (IsLocked(entity))                                     // 274
        {
            return Locked();
        }

        db.Events.Remove(entity);                                 // 279 — DELETE
        await db.SaveChangesAsync(ct);                            // 280
        posterStorage.Delete(entity.PosterPath);                  // 281 — удаляем файл постера с диска

        return Results.NoContent();                               // 283 — 204 без тела
    }
```

Файл удаляется **после** успешного `SaveChanges`, чтобы при ошибке БД не потерять
постер.

### `POST /api/events/{id}/poster`

```csharp
    private static async Task<IResult> UploadPoster(
        int id, IFormFile? file, HttpContext http, AppDbContext db,
        PosterStorage posterStorage, CancellationToken ct)
    {
        var userId = http.User.GetUserId();                       // 294
        if (userId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход"); // 297
        }

        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct); // 300
        if (entity is null)                                       // 301 → 404
        {
            return NotFound(id);
        }

        if (entity.AuthorId != userId)                            // 306 → 403
        {
            return Forbidden();
        }

        if (IsLocked(entity))                                     // 311 → 400
        {
            return Locked();
        }

        var (path, error) = await posterStorage.SaveAsync(file, ct); // 316 — проверка и запись файла
        if (path is null)                                         // 317 — ошибка валидации файла
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> // 319 — 400 + errors["file"]
            {
                ["file"] = [error!],                              // 321
            });
        }

        posterStorage.Delete(entity.PosterPath);                  // 325 — удаляем старый постер
        entity.PosterPath = path;                                 // 326 — сохраняем новый путь
        await db.SaveChangesAsync(ct);                            // 327

        return Results.Ok(new PosterUploadedDto(path));           // 329 — 200 + URL
    }
```

`IFormFile? file` привязывается к полю формы с именем `file` (см. `afisha.http` и
Angular `FormData.append('file', file)`).

### Вспомогательные методы

```csharp
    private static EventDetailsDto ToDetails(Event entity) => new( // 332 — сущность → DTO
        entity.Id,
        entity.Title,
        entity.Description,
        DateTime.SpecifyKind(entity.StartsAt, DateTimeKind.Utc),  // 336 — чтобы в JSON был "Z"
        entity.Price,
        entity.Seats,
        new VenueDto(entity.Venue.Id, entity.Venue.Name, entity.Venue.Address, entity.Venue.City), // 339
        entity.PosterPath,
        entity.AuthorId,
        entity.Author.DisplayName,                                // 342 — имя автора
        DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc));// 343

    private static bool IsLocked(Event entity) =>                // 345 — особое правило варианта
        entity.StartsAt - DateTime.UtcNow < EditLockWindow;       // до начала меньше 24 часов

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch // 347 — приводим дату к UTC
    {
        DateTimeKind.Utc => value,                                // 349 — уже UTC
        DateTimeKind.Local => value.ToUniversalTime(),            // 350 — локальное → UTC
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),       // 351 — без Kind считаем, что это UTC
    };

    private static IResult NotFound(int id) => Results.Problem(   // 354 — 404 ProblemDetails
        statusCode: StatusCodes.Status404NotFound,
        title: "Мероприятие не найдено",
        detail: $"Мероприятие с id = {id} не существует.");

    private static IResult Forbidden() => Results.Problem(        // 359 — 403
        statusCode: StatusCodes.Status403Forbidden,
        title: "Нет прав на это действие",
        detail: "Изменять и удалять мероприятие может только его автор.");

    private static IResult Locked() => Results.Problem(           // 364 — 400 за < 24 часов
        statusCode: StatusCodes.Status400BadRequest,
        title: "Мероприятие заблокировано",
        detail: "До начала осталось меньше 24 часов: изменения и удаление запрещены.");
}
```

---

## Миграции

Файлы в `Migrations/` сгенерированы EF Core, их не пишут руками.

### `20261104185625_InitialCreate.cs`

```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)   // применение миграции
    {
        migrationBuilder.CreateTable(name: "Venues", ...);          // CREATE TABLE Venues
        migrationBuilder.CreateTable(name: "Users", ...);           // CREATE TABLE Users
        migrationBuilder.CreateTable(name: "Events", ...);          // CREATE TABLE Events + FK + индексы
        migrationBuilder.InsertData(table: "Venues", ...);          // HasData: 5 площадок
        migrationBuilder.InsertData(table: "Users", ...);           // HasData: 2 пользователя
        migrationBuilder.InsertData(table: "Events", ...);          // HasData: 8 событий
    }

    protected override void Down(MigrationBuilder migrationBuilder) // откат
    {
        migrationBuilder.DropTable(name: "Events");                 // удаляем в обратном порядке
        migrationBuilder.DropTable(name: "Users");
        migrationBuilder.DropTable(name: "Venues");
    }
}
```

`InsertData` — это и есть `HasData`: данные лежат прямо в миграции, поэтому после
`Migrate()` база уже заполнена. `Designer.cs` и `AppDbContextModelSnapshot.cs` —
служебные: EF сравнивает текущую модель со снимком и понимает, что изменилось.

### `20261104185627_AddEventCreatedAtIndex.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateIndex(                 // вторая миграция добавляет индекс
        name: "IX_Events_CreatedAt",
        table: "Events",
        column: "CreatedAt");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex(name: "IX_Events_CreatedAt", table: "Events"); // откат
}
```

Как добавить третью миграцию:

```bash
cd server/Afisha.Api
dotnet ef migrations add НазваниеИзменения
dotnet ef database update
```

---

## Что спросить у себя после этого раздела

1. Где именно проверяется, что пользователь — автор?
2. Почему `AuthorId` берётся из токена, а не из тела запроса?
3. Почему `Include` нужен в `GetEventById`, но не в `DeleteEvent`?
4. Почему `CountAsync` вызывается до `Skip/Take`?
5. Что вернёт `DELETE`, если запись есть, но до начала 10 часов?
6. Какие коды вернёт `POST /api/events` без токена и с невалидным телом?
7. Зачем `DisableAntiforgery()` на загрузке файла?
8. Что делает `Results.Created(...)`?
9. Чем `Results.Ok` отличается от `Results.NoContent`?
10. Почему миграции не редактируют вручную?
