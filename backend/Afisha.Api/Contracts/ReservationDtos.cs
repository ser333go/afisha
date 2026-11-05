namespace Afisha.Api.Contracts;

/// <summary>Сколько мест забронировать.</summary>
public sealed record ReservationRequest(int Seats);

public sealed record ReservationDto(
    int Id,
    int EventId,
    string EventTitle,
    DateTime StartsAt,
    string VenueName,
    int Seats,
    DateTime CreatedAt);
