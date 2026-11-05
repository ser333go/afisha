# 09. Роли: администратор и обычный пользователь

Это дополнение к разбору. В версии из документов 01–08 права проверялись по автору
записи. Теперь управление афишей доступно **только роли `Admin`**, а обычный
пользователь может смотреть и бронировать места (но не менять афишу). Ниже — что именно изменилось и как это работает.

## Матрица доступа

| Действие | Гость | User | Admin |
| --- | --- | --- | --- |
| Смотреть список и страницу события | ✅ | ✅ | ✅ |
| Забронировать места (до начала) | 401 | 201 | 201 |
| Смотреть и отменять свои брони | — | ✅ (`/my-bookings`) | ✅ |
| Открыть `/events/new` и `/events/:id/edit` | редирект на вход | редирект на `/events` | ✅ |
| `POST /api/events` | 401 | 403 | 201 |
| `PUT /api/events/{id}` | 401 | 403 | 200 |
| `DELETE /api/events/{id}` | 401 | 403 | 204 |
| `POST /api/events/{id}/poster` | 401 | 403 | 200 |
| Изменить запись за < 24 часов | — | — | 400 |

Логин администратора: **`admin` / `admin`**.

## 1. Поле `Role` у пользователя

`backend/Afisha.Api/Models/User.cs`:

```csharp
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Роль: "Admin" — может менять афишу, "User" — только смотреть.</summary>
    public string Role { get; set; } = "User";

    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
```

В `AppDbContext` роль обязательна и ограничена 20 символами:

```csharp
user.Property(u => u.Role).IsRequired().HasMaxLength(20).HasDefaultValue("User");
```

Миграция `AddUserRoles` добавляет колонку `Role TEXT NOT NULL DEFAULT 'User'`.

## 2. Администратор создаётся при старте

В `HasData` администратора нет: в существующей базе Id 3 уже мог быть занят
реальным пользователем. Поэтому `Program.cs` после миграций проверяет, есть ли
хоть один `Admin`, и создаёт его:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any(u => u.Role == "Admin"))
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Users.Add(new User
        {
            Email = "admin",
            DisplayName = "Администратор",
            Role = "Admin",
            PasswordHash = hasher.Hash("admin"),
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }
}
```

Пароль `admin` хранится только PBKDF2-хешем. Логин — `admin`, потому что поле
`Email` используется как идентификатор входа.

## 3. Роль попадает в JWT

`JwtTokenService` добавляет claim `role`:

```csharp
var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
    new(JwtRegisteredClaimNames.Email, user.Email),
    new(JwtRegisteredClaimNames.Name, user.DisplayName),
    new("role", user.Role),
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
};
```

В `Program.cs` у проверки токена указано, что роль лежит именно в claim `role`:

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    // ...
    RoleClaimType = "role",
    NameClaimType = "name",
};
```

Без `RoleClaimType` `IsInRole("Admin")` не нашёл бы роль, потому что
`MapInboundClaims = false`.

## 4. Политика `AdminOnly`

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
```

Навешивается на все изменяющие операции:

```csharp
group.MapPost("/", CreateEvent)
    .RequireAuthorization("AdminOnly")
    // ...
```

Anonymous получает **401**, авторизованный `User` — **403**. Тексты 401/403
по-прежнему пишутся в `OnChallenge`/`OnForbidden` в виде ProblemDetails.

## 5. Проверки автора убраны

Раньше в `PUT`/`DELETE`/`POST poster` было:

```csharp
if (entity.AuthorId != userId) return Forbidden();
```

Теперь доступ решает политика `AdminOnly`, поэтому эти проверки удалены:
администратор может редактировать любую запись, в том числе созданную раньше
другим пользователем. Правило 24 часов осталось во всех трёх обработчиках:

```csharp
if (IsLocked(entity)) return Locked(); // 400, если до начала < 24 ч
```

При создании `AuthorId` по-прежнему берётся из токена:

```csharp
AuthorId = userId.Value, // id администратора, который создал запись
```

## 6. Клиент

`AuthService` знает роль:

```ts
readonly isAdmin = computed(() => this.userSignal()?.role === 'Admin');
```

`adminGuard` не пускает обычного пользователя на формы:

```ts
export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  if (auth.isAdmin()) {
    return true;
  }

  return router.createUrlTree(['/events']);
};
```

Маршруты:

```ts
{ path: 'events/new', canActivate: [adminGuard], /* ... */ },
{ path: 'events/:id/edit', canActivate: [adminGuard], /* ... */ },
```

Кнопки в карточке и на странице события показываются по `canManage`:

```ts
readonly canManage = computed(() => this.auth.isAdmin());
```

В шапке ссылка «Добавить» видна только администратору:

```html
@if (auth.isAdmin()) {
  <a routerLink="/events/new" routerLinkActive="is-active">Добавить</a>
}
```

## 7. Как проверить

```bash
# 1. Вход админа → role = Admin
curl -s -X POST http://localhost:5055/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin","password":"admin"}'

# 2. 401 без токена
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5055/api/events \
  -H 'Content-Type: application/json' \
  -d '{"title":"Без входа","startsAt":"2027-09-01T12:00:00Z","price":0,"seats":10,"venueId":1}'

# 3. 403 обычным пользователем
#    сначала логин demo@afisha.local / Demo12345, затем POST с его токеном
```

В браузере:

1. войти как `admin/admin` — видны «Добавить», «Редактировать», «Удалить»,
   «Загрузить постер»;
2. выйти и войти как `demo@afisha.local/Demo12345` — кнопок нет, а прямой переход
   на `/events/new` перенаправляет на `/events`.

## 8. Что это меняет в документах 01–08

- Проверки «только автор» в `03-server-endpoints.md` заменены политикой
  `AdminOnly` — актуальный код в этом файле.
- У `UserDto` появилось поле `Role`, в JWT — claim `role`.
- В клиенте `authGuard` для форм заменён на `adminGuard`, а `isAuthor` — на
  `canManage` (роль `Admin`).
- Номера строк в старых документах могли сдвинуться; ориентируйтесь на код и
  этот файл.
