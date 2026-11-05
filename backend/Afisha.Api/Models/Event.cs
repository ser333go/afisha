namespace Afisha.Api.Models;

/// <summary>Мероприятие афиши.</summary>
public class Event
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Дата и время начала, всегда хранится в UTC.</summary>
    public DateTime StartsAt { get; set; }

    /// <summary>Цена билета; 0 — бесплатно.</summary>
    public decimal Price { get; set; }

    /// <summary>Количество мест.</summary>
    public int Seats { get; set; }

    /// <summary>Относительный путь к загруженному постеру (например, /uploads/posters/{guid}.jpg).</summary>
    public string? PosterPath { get; set; }

    public int VenueId { get; set; }

    public Venue Venue { get; set; } = null!;

    /// <summary>Автор записи — только он может менять и удалять мероприятие.</summary>
    public int AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
