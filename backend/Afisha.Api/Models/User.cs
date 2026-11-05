namespace Afisha.Api.Models;

public class User
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Роль: "Admin" — может менять афишу, "User" — только смотреть.</summary>
    public string Role { get; set; } = "User";

    /// <summary>PBKDF2-хеш пароля. Открытый пароль не хранится нигде.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Event> Events { get; set; } = new List<Event>();

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
