using Afisha.Api.Auth;
using Afisha.Api.Contracts;
using Afisha.Api.Data;
using Afisha.Api.Models;
using Afisha.Api.Storage;
using Afisha.Api.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Afisha.Api.Endpoints;

public static class EventEndpoints
{
    private static readonly TimeSpan EditLockWindow = TimeSpan.FromHours(24);

    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapGet("/", GetEvents)
            .WithName("GetEvents")
            .WithSummary("Список мероприятий: страницы, фильтры, сортировка")
            .Produces<PagedResult<EventListItemDto>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:int}", GetEventById)
            .WithName("GetEventById")
            .WithSummary("Мероприятие по id")
            .Produces<EventDetailsDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateEvent)
            .RequireAuthorization("AdminOnly")
            .AddEndpointFilter<ValidationFilter<EventRequest>>()
            .WithName("CreateEvent")
            .WithSummary("Создать мероприятие (только администратор)")
            .Produces<EventDetailsDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:int}", UpdateEvent)
            .RequireAuthorization("AdminOnly")
            .AddEndpointFilter<ValidationFilter<EventRequest>>()
            .WithName("UpdateEvent")
            .WithSummary("Изменить мероприятие (только администратор, не позднее чем за 24 часа)")
            .Produces<EventDetailsDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", DeleteEvent)
            .RequireAuthorization("AdminOnly")
            .WithName("DeleteEvent")
            .WithSummary("Удалить мероприятие (только администратор, не позднее чем за 24 часа)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:int}/poster", UploadPoster)
            .RequireAuthorization("AdminOnly")
            .DisableAntiforgery()
            .WithName("UploadPoster")
            .WithSummary("Загрузить постер (только администратор, jpg/png/webp до 5 МБ)")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<PosterUploadedDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetEvents(
        AppDbContext db,
        IValidator<EventQuery> validator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 6,
        int? venueId = null,
        bool? free = null,
        DateOnly? date = null,
        string sort = "date",
        string order = "asc")
    {
        var query = new EventQuery(page, pageSize, venueId, free, date, sort, order);
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToErrorDictionary());
        }

        var events = db.Events.AsNoTracking();

        if (venueId is not null)
        {
            events = events.Where(e => e.VenueId == venueId);
        }

        if (free is true)
        {
            events = events.Where(e => e.Price == 0);
        }
        else if (free is false)
        {
            events = events.Where(e => e.Price > 0);
        }

        if (date is { } day)
        {
            var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var to = from.AddDays(1);
            events = events.Where(e => e.StartsAt >= from && e.StartsAt < to);
        }

        var total = await events.CountAsync(cancellationToken);
        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);

        var sorted = sort.ToLowerInvariant() switch
        {
            "price" => descending
                ? events.OrderByDescending(e => e.Price).ThenBy(e => e.StartsAt)
                : events.OrderBy(e => e.Price).ThenBy(e => e.StartsAt),
            _ => descending
                ? events.OrderByDescending(e => e.StartsAt).ThenBy(e => e.Id)
                : events.OrderBy(e => e.StartsAt).ThenBy(e => e.Id),
        };

        var items = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventListItemDto(
                e.Id,
                e.Title,
                DateTime.SpecifyKind(e.StartsAt, DateTimeKind.Utc),
                e.Price,
                e.Seats,
                e.Reservations.Sum(r => (int?)r.Seats) ?? 0,
                e.Seats - (e.Reservations.Sum(r => (int?)r.Seats) ?? 0),
                new VenueDto(e.Venue.Id, e.Venue.Name, e.Venue.Address, e.Venue.City),
                e.PosterPath,
                e.AuthorId))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResult<EventListItemDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> GetEventById(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Author)
            .Include(e => e.Reservations)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return entity is null ? NotFound(id) : Results.Ok(ToDetails(entity));
    }

    private static async Task<IResult> CreateEvent(
        EventRequest request,
        HttpContext http,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        if (userId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход");
        }

        var entity = new Event
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            StartsAt = NormalizeUtc(request.StartsAt),
            Price = request.Price,
            Seats = request.Seats,
            VenueId = request.VenueId,
            AuthorId = userId.Value,
            CreatedAt = DateTime.UtcNow,
        };

        db.Events.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var created = await db.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Author)
            .Include(e => e.Reservations)
            .FirstAsync(e => e.Id == entity.Id, cancellationToken);

        return Results.Created($"/api/events/{entity.Id}", ToDetails(created));
    }

    private static async Task<IResult> UpdateEvent(
        int id,
        EventRequest request,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.Events
            .Include(e => e.Venue)
            .Include(e => e.Author)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound(id);
        }

        if (IsLocked(entity))
        {
            return Locked();
        }

        var reservedSeats = await db.Reservations
            .Where(r => r.EventId == id)
            .SumAsync(r => (int?)r.Seats, cancellationToken) ?? 0;
        if (request.Seats < reservedSeats)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seats"] = [$"Уже забронировано {reservedSeats} мест. Нельзя уменьшить количество мест ниже брони."],
            });
        }

        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.StartsAt = NormalizeUtc(request.StartsAt);
        entity.Price = request.Price;
        entity.Seats = request.Seats;
        entity.VenueId = request.VenueId;

        await db.SaveChangesAsync(cancellationToken);

        // Навигация Venue могла измениться — перечитываем запись вместе со справочником.
        var updated = await db.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Author)
            .Include(e => e.Reservations)
            .FirstAsync(e => e.Id == id, cancellationToken);

        return Results.Ok(ToDetails(updated));
    }

    private static async Task<IResult> DeleteEvent(
        int id,
        AppDbContext db,
        PosterStorage posterStorage,
        CancellationToken cancellationToken)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound(id);
        }

        if (IsLocked(entity))
        {
            return Locked();
        }

        db.Events.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        posterStorage.Delete(entity.PosterPath);

        return Results.NoContent();
    }

    private static async Task<IResult> UploadPoster(
        int id,
        IFormFile? file,
        AppDbContext db,
        PosterStorage posterStorage,
        CancellationToken cancellationToken)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound(id);
        }

        if (IsLocked(entity))
        {
            return Locked();
        }

        var (path, error) = await posterStorage.SaveAsync(file, cancellationToken);
        if (path is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [error!],
            });
        }

        posterStorage.Delete(entity.PosterPath);
        entity.PosterPath = path;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new PosterUploadedDto(path));
    }

    private static EventDetailsDto ToDetails(Event entity) => new(
        entity.Id,
        entity.Title,
        entity.Description,
        DateTime.SpecifyKind(entity.StartsAt, DateTimeKind.Utc),
        entity.Price,
        entity.Seats,
        entity.Reservations.Sum(r => r.Seats),
        entity.Seats - entity.Reservations.Sum(r => r.Seats),
        new VenueDto(entity.Venue.Id, entity.Venue.Name, entity.Venue.Address, entity.Venue.City),
        entity.PosterPath,
        entity.AuthorId,
        entity.Author.DisplayName,
        DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc));

    private static bool IsLocked(Event entity) => entity.StartsAt - DateTime.UtcNow < EditLockWindow;

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static IResult NotFound(int id) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Мероприятие не найдено",
        detail: $"Мероприятие с id = {id} не существует.");

    private static IResult Locked() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Мероприятие заблокировано",
        detail: "До начала осталось меньше 24 часов: изменения и удаление запрещены.");
}
