using System.Text;
using Afisha.Api.Auth;
using Afisha.Api.Contracts;
using Afisha.Api.Data;
using Afisha.Api.Endpoints;
using Afisha.Api.Models;
using Afisha.Api.Storage;
using Afisha.Api.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// База лежит в папке database рядом с backend/frontend. Путь из appsettings.json
// относительный (../../database/afisha.db), поэтому превращаем его в абсолютный
// от content root — так приложение не зависит от текущей папки запуска.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=../../database/afisha.db";
var sqlite = new SqliteConnectionStringBuilder(connectionString);
if (!Path.IsPathRooted(sqlite.DataSource))
{
    sqlite.DataSource = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, sqlite.DataSource));
}

var databaseDirectory = Path.GetDirectoryName(sqlite.DataSource);
if (!string.IsNullOrEmpty(databaseDirectory))
{
    Directory.CreateDirectory(databaseDirectory);
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqlite.ToString()));

builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<PosterStorage>();

builder.Services.AddScoped<IValidator<EventRequest>, EventRequestValidator>();
builder.Services.AddScoped<IValidator<EventQuery>, EventQueryValidator>();
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<ReservationRequest>, ReservationRequestValidator>();

builder.Services.AddProblemDetails();

// Ключ подписи JWT обязан приходить из user-secrets (в git его нет).
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key не настроен. Выполните в папке server/Afisha.Api: " +
        "dotnet user-secrets set \"Jwt:Key\" \"<случайная строка длиной не менее 32 символов>\".");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Afisha.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Afisha.Client";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "name",
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Требуется вход",
                        detail: "Добавьте заголовок Authorization: Bearer <токен>.")
                    .ExecuteAsync(context.HttpContext);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Действие запрещено",
                        detail: "У вас нет прав на эту операцию.")
                    .ExecuteAsync(context.HttpContext);
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Политика для операций с афишей: только пользователь с ролью Admin.
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddCors(options => options.AddPolicy("spa", policy => policy
    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Гарантируем наличие администратора. В HasData его нет, чтобы Id не конфликтовал
    // с реальными пользователями, зарегистрированными в существующей базе.
    if (!db.Users.Any(u => u.Role == "Admin"))
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Users.Add(new User
        {
            Email = "admin",
            DisplayName = "Администратор",
            Role = "Admin",
            PasswordHash = hasher.Hash("admin"),
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "posters"));
}

app.UseExceptionHandler();
app.UseStaticFiles();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Afisha API"));
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapAuthEndpoints();
app.MapVenueEndpoints();
app.MapEventEndpoints();
app.MapReservationEndpoints();

app.Run();

public partial class Program;
