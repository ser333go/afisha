namespace Afisha.Api.Models;

/// <summary>Площадка — справочник, с которым связана главная сущность «мероприятие».</summary>
public class Venue
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
