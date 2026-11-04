using Afisha.Api.Contracts;
using Afisha.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Afisha.Api.Endpoints;

public static class VenueEndpoints
{
    public static RouteGroupBuilder MapVenueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/venues").WithTags("Venues");

        group.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var venues = await db.Venues
                .AsNoTracking()
                .OrderBy(v => v.Name)
                .Select(v => new VenueDto(v.Id, v.Name, v.Address, v.City))
                .ToListAsync(cancellationToken);

            return Results.Ok(venues);
        })
        .WithName("GetVenues")
        .WithSummary("Справочник площадок")
        .Produces<List<VenueDto>>();

        return group;
    }
}
