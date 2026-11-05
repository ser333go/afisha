namespace Afisha.Api.Models;

/// <summary>Бронь мест на мероприятие конкретным пользователем.</summary>
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
