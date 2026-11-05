# 10. Бронирование мест

Дополнение к разбору: любой вошедший пользователь может забронировать места на
будущее мероприятие, посмотреть свои брони и отменить их. Управление афишей при
этом остаётся только у администратора.

## Схема данных

```
Users 1 ─── N Reservations N ─── 1 Events
```

`backend/Afisha.Api/Models/Reservation.cs`:

```csharp
public class Reservation
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Сколько мест забронировано.</summary>
    public int Seats { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

В `AppDbContext`:

```csharp
public DbSet<Reservation> Reservations => Set<Reservation>();

modelBuilder.Entity<Reservation>(reservation =>
{
    reservation.ToTable("Reservations");
    reservation.HasOne(r => r.Event)
        .WithMany(e => e.Reservations)
        .HasForeignKey(r => r.EventId)
        .OnDelete(DeleteBehavior.Cascade); // удалили событие — удалились брони
    reservation.HasOne(r => r.User)
        .WithMany(u => u.Reservations)
        .HasForeignKey(r => r.UserId)
        .OnDelete(DeleteBehavior.Restrict);
    reservation.HasIndex(r => new { r.EventId, r.UserId }).IsUnique();
});
```

`ON DELETE CASCADE` — событие удаляется, брони уходят вместе с ним. Уникальный
индекс не даёт одному пользователю иметь две брони на одно событие.

Миграция: `AddReservations` создаёт таблицу и индексы.

## Сколько мест свободно

Свободно = всего мест − сумма забронированных:

```csharp
var reserved = await db.Reservations
    .Where(r => r.EventId == id)
    .SumAsync(r => (int?)r.Seats, ct) ?? 0;

var available = entity.Seats - reserved;
```

В списке событий это считается подзапросом в проекции:

```csharp
e.Seats,
e.Reservations.Sum(r => (int?)r.Seats) ?? 0,
e.Seats - (e.Reservations.Sum(r => (int?)r.Seats) ?? 0),
```

Именно поэтому в DTO появились `reservedSeats` и `availableSeats`.

## Бронирование

`POST /api/events/{id}/reservations`, тело `{ "seats": 2 }`, только с токеном.

Проверки в `ReservationEndpoints.Reserve`:

1. пользователь вошёл (`GetUserId`) — иначе 401;
2. событие существует — иначе 404;
3. событие ещё не началось — иначе 400 «Бронирование закрыто»;
4. запрошено не больше свободных мест — иначе 400 с `errors.seats`;
5. у пользователя ещё нет брони на это событие — иначе 400;
6. запись сохраняется в транзакции, ответ — `201 Created`.

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);

var reservedSeats = await db.Reservations
    .Where(r => r.EventId == id)
    .SumAsync(r => (int?)r.Seats, ct) ?? 0;
var availableSeats = entity.Seats - reservedSeats;

if (request.Seats > availableSeats)
{
    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["seats"] = [$"Свободных мест осталось: {availableSeats}."],
    });
}
```

Валидатор `ReservationRequestValidator` разрешает 1–50 мест, а сервер дополнительно
проверяет фактическое наличие.

## Мои брони

`GET /api/reservations/my` возвращает брони текущего пользователя:

```csharp
var items = await db.Reservations
    .AsNoTracking()
    .Where(r => r.UserId == userId)
    .OrderBy(r => r.Event.StartsAt)
    .Select(r => new ReservationDto(
        r.Id, r.EventId, r.Event.Title,
        DateTime.SpecifyKind(r.Event.StartsAt, DateTimeKind.Utc),
        r.Event.Venue.Name, r.Seats,
        DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)))
    .ToListAsync(ct);
```

## Отмена

`DELETE /api/reservations/{id}`:

- 404 — брони нет;
- 403 — бронь принадлежит другому пользователю;
- 204 — своя бронь удалена.

## Ограничение для администратора

Если администратор уменьшает `Seats` у события, сервер проверяет, что новое
значение не меньше суммы броней:

```csharp
var reservedSeats = await db.Reservations
    .Where(r => r.EventId == id)
    .SumAsync(r => (int?)r.Seats, ct) ?? 0;

if (request.Seats < reservedSeats)
{
    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["seats"] = [$"Уже забронировано {reservedSeats} мест..."],
    });
}
```

Иначе места «исчезли» бы из-под уже забронировавших.

## Клиент

- `EventService.myReservationsResource()` — `httpResource` на `/api/reservations/my`,
  запрос идёт только если пользователь вошёл.
- `EventService.reserve()` — POST, `cancelReservation()` — DELETE.
- На странице события: блок «Занять места», количество свободных мест, форма,
  кнопка «Отменить бронь» для своей брони.
- Страница `/my-bookings` — «Мои брони»: список, даты, места, отмена; четыре
  состояния (загрузка/ошибка/пусто/данные).
- В карточке списка выводится «X из Y свободно».

## Как проверить

```bash
# вход обычным пользователем
TOKEN=$(curl -s -X POST http://localhost:5055/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"demo@afisha.local","password":"Demo12345"}' | jq -r .token)

# занять 2 места
curl -s -X POST http://localhost:5055/api/events/2/reservations \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"seats":2}'

# мои брони
curl -s http://localhost:5055/api/reservations/my -H "Authorization: Bearer $TOKEN"

# отмена (id из ответа выше)
curl -i -X DELETE http://localhost:5055/api/reservations/1 -H "Authorization: Bearer $TOKEN"
```

В браузере: войти как `demo@afisha.local` → открыть событие → «Забронировать» →
«Мои брони» в шапке → «Отменить».
