namespace Afisha.Api.Contracts;

public sealed record EventListItemDto(
    int Id,
    string Title,
    DateTime StartsAt,
    decimal Price,
    int Seats,
    int ReservedSeats,
    int AvailableSeats,
    VenueDto Venue,
    string? PosterUrl,
    int AuthorId);

public sealed record EventDetailsDto(
    int Id,
    string Title,
    string? Description,
    DateTime StartsAt,
    decimal Price,
    int Seats,
    int ReservedSeats,
    int AvailableSeats,
    VenueDto Venue,
    string? PosterUrl,
    int AuthorId,
    string AuthorName,
    DateTime CreatedAt);

/// <summary>Тело запроса для создания и редактирования мероприятия.</summary>
public sealed record EventRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    decimal Price,
    int Seats,
    int VenueId);

/// <summary>Параметры списка: страницы, фильтры и сортировка.</summary>
public sealed record EventQuery(
    int Page = 1,
    int PageSize = 6,
    int? VenueId = null,
    bool? Free = null,
    DateOnly? Date = null,
    string Sort = "date",
    string Order = "asc");
