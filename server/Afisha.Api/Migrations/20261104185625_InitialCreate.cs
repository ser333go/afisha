using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Afisha.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Price = table.Column<long>(type: "INTEGER", nullable: false),
                    Seats = table.Column<int>(type: "INTEGER", nullable: false),
                    PosterPath = table.Column<string>(type: "TEXT", nullable: true),
                    VenueId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "Email", "PasswordHash" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Демо-автор", "demo@afisha.local", "PBKDF2$100000$+Zvdd1T8NY9QwmJAoNz1bA==$+Izq8RKzDap0a1JJGgDdIPivL830Ot9ICLw35Wt8h0k=" },
                    { 2, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Второй автор", "guest@afisha.local", "PBKDF2$100000$XHQguj2i9735t7U1mfcnkg==$7sMNtwXrbhgoOOzuKURdTPrDAIqWgx2twW/o0eo2H3k=" }
                });

            migrationBuilder.InsertData(
                table: "Venues",
                columns: new[] { "Id", "Address", "City", "Name" },
                values: new object[,]
                {
                    { 1, "ул. Амира Темура, 12", "Ташкент", "Дом культуры «Галактика»" },
                    { 2, "ул. Шота Руставели, 8", "Ташкент", "Клуб «Фонарь»" },
                    { 3, "ул. Пахтакорская, 5", "Ташкент", "Театр «Ильхом»" },
                    { 4, "пр. Мустакиллик, 3", "Ташкент", "Выставочный зал «Хумо»" },
                    { 5, "ул. Анхор, 1", "Ташкент", "Парк «Анхор»" }
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "Id", "AuthorId", "CreatedAt", "Description", "PosterPath", "Price", "Seats", "StartsAt", "Title", "VenueId" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Музыкально-визуальное шоу под куполом: живой оркестр, проекции туманностей и лекция о телескопе «Джеймс Уэбб».", null, 12000000L, 200, new DateTime(2027, 2, 14, 13, 0, 0, 0, DateTimeKind.Utc), "Ночь космоса", 1 },
                    { 2, 1, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Квартет саксофониста Алишера Каримова играет стандарты и собственные пьесы. Открытая крышка, пледы и глинтвейн.", null, 18000000L, 80, new DateTime(2027, 3, 6, 14, 0, 0, 0, DateTimeKind.Utc), "Джаз на крыше", 2 },
                    { 3, 2, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Новая редакция мольеровской комедии в постановке театра «Ильхом». Продолжительность — 2 часа 20 минут с антрактом.", null, 9000000L, 120, new DateTime(2027, 3, 21, 10, 0, 0, 0, DateTimeKind.Utc), "Ильхом: «Тартюф»", 3 },
                    { 4, 1, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Работы 18 художников Узбекистана: живопись, керамика и медиаинсталляции. Вход свободный, регистрация не нужна.", null, 0L, 500, new DateTime(2027, 4, 2, 4, 0, 0, 0, DateTimeKind.Utc), "Выставка современного искусства", 4 },
                    { 5, 2, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Показ фильма «Дом у озера» на большом экране под открытым небом. Бесплатно, приносите пледы.", null, 0L, 300, new DateTime(2027, 4, 18, 14, 0, 0, 0, DateTimeKind.Utc), "Open-air кино в «Анхоре»", 5 },
                    { 6, 1, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Симфонический оркестр исполняет Чайковского и Равеля. Дирижёр — Тимур Абдуллаев.", null, 25000000L, 400, new DateTime(2027, 5, 9, 11, 0, 0, 0, DateTimeKind.Utc), "Симфонический вечер", 1 },
                    { 7, 2, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Восемь комиков, один микрофон и час импровизации. Лучшая шутка вечера получает приз зрительских симпатий.", null, 6000000L, 60, new DateTime(2027, 5, 23, 13, 0, 0, 0, DateTimeKind.Utc), "Стендап: открытый микрофон", 2 },
                    { 8, 1, new DateTime(2026, 10, 1, 12, 0, 0, 0, DateTimeKind.Utc), "Музыкальная сказка по мотивам Корнея Чуковского для зрителей от 3 лет.", null, 4500000L, 150, new DateTime(2027, 6, 1, 5, 0, 0, 0, DateTimeKind.Utc), "Детский спектакль «Айболит»", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_AuthorId",
                table: "Events",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Price",
                table: "Events",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Events_StartsAt",
                table: "Events",
                column: "StartsAt");

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId",
                table: "Events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Venues");
        }
    }
}
