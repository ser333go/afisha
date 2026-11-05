using Afisha.Api.Models;

namespace Afisha.Api.Data;

/// <summary>Начальные данные, которые попадают в базу через HasData.</summary>
public static class SeedData
{
    private static readonly DateTime SeededAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public static readonly Venue[] Venues =
    [
        new() { Id = 1, Name = "Дом культуры «Галактика»", Address = "ул. Амира Темура, 12", City = "Ташкент" },
        new() { Id = 2, Name = "Клуб «Фонарь»", Address = "ул. Шота Руставели, 8", City = "Ташкент" },
        new() { Id = 3, Name = "Театр «Ильхом»", Address = "ул. Пахтакорская, 5", City = "Ташкент" },
        new() { Id = 4, Name = "Выставочный зал «Хумо»", Address = "пр. Мустакиллик, 3", City = "Ташкент" },
        new() { Id = 5, Name = "Парк «Анхор»", Address = "ул. Анхор, 1", City = "Ташкент" },
    ];

    public static readonly User[] Users =
    [
        new()
        {
            Id = 1,
            Email = "demo@afisha.local",
            DisplayName = "Демо-пользователь",
            Role = "User",
            PasswordHash = "PBKDF2$100000$+Zvdd1T8NY9QwmJAoNz1bA==$+Izq8RKzDap0a1JJGgDdIPivL830Ot9ICLw35Wt8h0k=",
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 2,
            Email = "guest@afisha.local",
            DisplayName = "Второй пользователь",
            Role = "User",
            PasswordHash = "PBKDF2$100000$XHQguj2i9735t7U1mfcnkg==$7sMNtwXrbhgoOOzuKURdTPrDAIqWgx2twW/o0eo2H3k=",
            CreatedAt = SeededAt,
        },
    ];

    public static readonly Event[] Events =
    [
        new()
        {
            Id = 1,
            Title = "Ночь космоса",
            Description = "Музыкально-визуальное шоу под куполом: живой оркестр, проекции туманностей и лекция о телескопе «Джеймс Уэбб».",
            StartsAt = new DateTime(2027, 2, 14, 13, 0, 0, DateTimeKind.Utc),
            Price = 120_000m,
            Seats = 200,
            VenueId = 1,
            AuthorId = 1,
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 2,
            Title = "Джаз на крыше",
            Description = "Квартет саксофониста Алишера Каримова играет стандарты и собственные пьесы. Открытая крышка, пледы и глинтвейн.",
            StartsAt = new DateTime(2027, 3, 6, 14, 0, 0, DateTimeKind.Utc),
            Price = 180_000m,
            Seats = 80,
            VenueId = 2,
            AuthorId = 1,
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 3,
            Title = "Ильхом: «Тартюф»",
            Description = "Новая редакция мольеровской комедии в постановке театра «Ильхом». Продолжительность — 2 часа 20 минут с антрактом.",
            StartsAt = new DateTime(2027, 3, 21, 10, 0, 0, DateTimeKind.Utc),
            Price = 90_000m,
            Seats = 120,
            VenueId = 3,
            AuthorId = 2,
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 4,
            Title = "Выставка современного искусства",
            Description = "Работы 18 художников Узбекистана: живопись, керамика и медиаинсталляции. Вход свободный, регистрация не нужна.",
            StartsAt = new DateTime(2027, 4, 2, 4, 0, 0, DateTimeKind.Utc),
            Price = 0m,
            Seats = 500,
            VenueId = 4,
            AuthorId = 1,
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 5,
            Title = "Open-air кино в «Анхоре»",
            Description = "Показ фильма «Дом у озера» на большом экране под открытым небом. Бесплатно, приносите пледы.",
            StartsAt = new DateTime(2027, 4, 18, 14, 0, 0, DateTimeKind.Utc),
            Price = 0m,
            Seats = 300,
            VenueId = 5,
            AuthorId = 2,
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 6,
            Title = "Симфонический вечер",
            Description = "Симфонический оркестр исполняет Чайковского и Равеля. Дирижёр — Тимур Абдуллаев.",
            StartsAt = new DateTime(2027, 5, 9, 11, 0, 0, DateTimeKind.Utc),
            Price = 250_000m,
            Seats = 400,
            VenueId = 1,
            AuthorId = 1,
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 7,
            Title = "Стендап: открытый микрофон",
            Description = "Восемь комиков, один микрофон и час импровизации. Лучшая шутка вечера получает приз зрительских симпатий.",
            StartsAt = new DateTime(2027, 5, 23, 13, 0, 0, DateTimeKind.Utc),
            Price = 60_000m,
            Seats = 60,
            VenueId = 2,
            AuthorId = 2,
            CreatedAt = SeededAt,
        },
        new()
        {
            Id = 8,
            Title = "Детский спектакль «Айболит»",
            Description = "Музыкальная сказка по мотивам Корнея Чуковского для зрителей от 3 лет.",
            StartsAt = new DateTime(2027, 6, 1, 5, 0, 0, DateTimeKind.Utc),
            Price = 45_000m,
            Seats = 150,
            VenueId = 3,
            AuthorId = 1,
            CreatedAt = SeededAt,
        },
    ];
}
