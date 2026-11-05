using System.IdentityModel.Tokens.Jwt;
using Afisha.Api.Auth;
using Afisha.Api.Contracts;
using Afisha.Api.Data;
using Afisha.Api.Models;
using Afisha.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace Afisha.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .WithName("Register")
            .WithSummary("Регистрация")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/login", Login)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithName("Login")
            .WithSummary("Вход, выдаёт JWT")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapGet("/me", Me)
            .RequireAuthorization()
            .WithName("Me")
            .WithSummary("Текущий пользователь")
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        AppDbContext db,
        IPasswordHasher passwordHasher,
        JwtTokenService jwt,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Пользователь с таким email уже зарегистрирован."],
            });
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = "User",
            PasswordHash = passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        var (token, _) = jwt.CreateToken(user);
        var response = new AuthResponse(token, new UserDto(user.Id, user.Email, user.DisplayName, user.Role));
        return Results.Created("/api/auth/me", response);
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        AppDbContext db,
        IPasswordHasher passwordHasher,
        JwtTokenService jwt,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Неверный email или пароль",
                detail: "Проверьте введённые данные и попробуйте снова.");
        }

        var (token, _) = jwt.CreateToken(user);
        return Results.Ok(new AuthResponse(token, new UserDto(user.Id, user.Email, user.DisplayName, user.Role)));
    }

    private static async Task<IResult> Me(
        HttpContext http,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId();
        if (userId is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Требуется вход");
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Пользователь не найден");
        }

        return Results.Ok(new UserDto(user.Id, user.Email, user.DisplayName, user.Role));
    }
}
