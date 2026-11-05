using Afisha.Api.Auth;
using Afisha.Api.Contracts;
using Afisha.Api.Data;
using Afisha.Api.Models;
using Afisha.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace Afisha.Api.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        // Забронировать места на событие может любой вошедший пользователь.
        app.MapPost("/api/events/{id:int}/reservations", Reserve)
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<ReservationRequest>>()
            .WithTags("Reservations")
            .WithName("ReserveSeats")
            .WithSummary("Забронировать места на мероприятие")
            .Produces<ReservationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var group = app.MapGroup("/api/reservations").WithTags("Reservations");

        group.MapGet("/my", GetMyReservations)
            .RequireAuthorization()
            .WithName("GetMyReservations")
            .WithSummary("Мои брони")
            .Produces<List<ReservationDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{id:int}", CancelReservation)
            .RequireAuthorization()
            .WithName("CancelReservation")
            .WithSummary("Отменить свою бронь")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Reserve(
        int id,
        ReservationRequest request,
        HttpContext http,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        if (userId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход");
        }

        var entity = await db.Events
            .Include(e => e.Venue)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Мероприятие не найдено",
                detail: $"Мероприятие с id = {id} не существует.");
        }

        if (entity.StartsAt <= DateTime.UtcNow)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Бронирование закрыто",
                detail: "Событие уже началось, забронировать места нельзя.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var reservedSeats = await db.Reservations
            .Where(r => r.EventId == id)
            .SumAsync(r => (int?)r.Seats, cancellationToken) ?? 0;
        var availableSeats = entity.Seats - reservedSeats;

        if (request.Seats > availableSeats)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seats"] = [$"Свободных мест осталось: {availableSeats}."],
            });
        }

        if (await db.Reservations.AnyAsync(
                r => r.EventId == id && r.UserId == userId, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seats"] = ["У вас уже есть бронь на это событие. Сначала отмените её."],
            });
        }

        var reservation = new Reservation
        {
            EventId = id,
            UserId = userId.Value,
            Seats = request.Seats,
            CreatedAt = DateTime.UtcNow,
        };

        db.Reservations.Add(reservation);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seats"] = ["Не удалось забронировать места. Попробуйте ещё раз."],
            });
        }

        var dto = new ReservationDto(
            reservation.Id,
            entity.Id,
            entity.Title,
            DateTime.SpecifyKind(entity.StartsAt, DateTimeKind.Utc),
            entity.Venue.Name,
            reservation.Seats,
            DateTime.SpecifyKind(reservation.CreatedAt, DateTimeKind.Utc));

        return Results.Created($"/api/reservations/{reservation.Id}", dto);
    }

    private static async Task<IResult> GetMyReservations(
        HttpContext http,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        if (userId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход");
        }

        var items = await db.Reservations
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Event.StartsAt)
            .Select(r => new ReservationDto(
                r.Id,
                r.EventId,
                r.Event.Title,
                DateTime.SpecifyKind(r.Event.StartsAt, DateTimeKind.Utc),
                r.Event.Venue.Name,
                r.Seats,
                DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> CancelReservation(
        int id,
        HttpContext http,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        if (userId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход");
        }

        var reservation = await db.Reservations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reservation is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Бронь не найдена");
        }

        if (reservation.UserId != userId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Это чужая бронь",
                detail: "Отменить можно только свою бронь.");
        }

        db.Reservations.Remove(reservation);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
