# 08. Подготовка к защите

## План демонстрации на 10 минут

| Время | Что показать | Что сказать |
| --- | --- | --- |
| 0:00–0:30 | `README.md`, структуру папок | «Вариант — афиша: события и справочник площадок» |
| 0:30–1:30 | `/scalar` | «OpenAPI показывает все методы: events, auth, venues, upload» |
| 1:30–3:00 | `/events`: фильтр по площадке, «бесплатные», дата, сортировка по цене, «Назад/Вперёд» | «page/pageSize/total, фильтры и две сортировки из задания» |
| 3:00–3:40 | карточка → страница события | «Страница одной записи, постер, роль» |
| 3:40–4:40 | вход обычным пользователем (`demo@afisha.local`), открыть событие | «Кнопок управления нет; сервер всё равно проверит 403» |
| 4:40–5:20 | `.http` №18 (PUT токеном обычного пользователя) | «403 — не администратор» |
| 5:20–6:20 | вход `admin/admin`, создание события | «201, роль Admin из JWT» |
| 6:20–7:10 | загрузка постера | «до 5 МБ, jpg/png/webp, имя — GUID» |
| 7:10–7:50 | бронь места обычным пользователем и «Мои брони» | «201, свободные места, отмена брони» |
| 7:50–8:20 | форма с ошибками (пусто/прошлое/цена −5) | «клиентская валидация + серверные errors под полями» |
| 8:20–9:00 | `.http` №22–24: событие через 2 часа → PUT/DELETE | «400: правило 24 часов» |
| 9:00–9:30 | `.http` №17 (без токена) и №21 (404) | «401 и 404» |
| 9:30–10:00 | `Program.cs`, `EventEndpoints.cs`, `AppDbContext.cs` | «готов объяснить любые строки» |

---

## 26 вопросов-ответов

**1. Почему Minimal API, а не контроллеры?**
Так короче: маршрут и обработчик в одном месте, меньше церемоний. Группы
`MapGroup("/api/events")` дают общий префикс и теги для OpenAPI.

**2. Зачем DTO, если есть сущности?**
Чтобы не отдавать наружу лишнее (`PasswordHash`) и не привязывать контракт API к схеме
БД. DTO — `record`, они неизменяемые.

**3. Как работает DI?**
`builder.Services.AddScoped<...>()` регистрирует тип. Потом ASP.NET сам создаёт
экземпляр и передаёт в параметры обработчика/конструктор. `Scoped` — один на запрос,
`Singleton` — один на приложение.

**4. Почему `DbContext` — Scoped?**
Он не потокобезопасен и копит изменения. На каждый запрос — свой контекст, после
запроса освобождается.

**5. Как LINQ превращается в SQL?**
`db.Events.Where(...).OrderBy(...).Select(...)` — это `IQueryable`, описание запроса.
SQL выполняется только на `ToListAsync/CountAsync/FirstOrDefaultAsync`. До этого
можно навешивать условия.

**6. Зачем миграции?**
Это версионирование схемы БД. `InitialCreate` создаёт таблицы и сеет данные,
`AddEventCreatedAtIndex` добавляет индекс. `Database.Migrate()` применяет недостающие.

**7. Чем `HasData` отличается от сидирования в коде?**
`HasData` попадает прямо в миграцию (`InsertData`) и не выполняется каждый старт.
Обычный код с `if (!db.Events.Any())` выполнялся бы при каждом запуске.

**8. Почему цена хранится как `long`, а не `decimal`?**
SQLite не имеет decimal. EF хранит decimal как TEXT и сравнение/сортировка могут быть
нечисловыми. Конвертер `decimal → long` (копейки) даёт правильную сортировку.

**9. Почему даты в UTC?**
Сервер не знает часовой пояс клиента. UTC — общая точка отсчёта; клиент показывает
локальное время. Конвертер помечает прочитанные даты как `Utc`, чтобы JSON был с `Z`.

**10. Как работает валидация?**
`ValidationFilter<T>` находит DTO среди аргументов, вызывает
`IValidator<T>.ValidateAsync`. Если ошибки — возвращает `Results.ValidationProblem`,
это 400 + ProblemDetails + `errors`.

**11. Что такое ProblemDetails?**
Стандартный формат ошибки: `type`, `title`, `status`, `detail`, `traceId`, `errors`.
Клиент читает `errors` и раскладывает по полям.

**12. Как хранится пароль?**
PBKDF2-HMAC-SHA256, 100 000 итераций, случайная соль 16 байт, 32 байта хеша. Формат
`PBKDF2$100000$salt$hash`. Открытый пароль не хранится.

**13. Зачем соль?**
Одинаковые пароли дают разные хеши, а радужные таблицы бесполезны. Соль хранится
рядом с хешем — это нормально.

**14. Что внутри JWT?**
`sub` (id), `email`, `name`, `jti`, `nbf`, `exp`, `iss`, `aud` и подпись. Payload
не шифруется — его можно прочитать; защищает подпись.

**15. Откуда ключ подписи?**
Из user-secrets (`Jwt:Key`), вне git. Если ключа нет — приложение падает при старте
с подсказкой. Для проверки подписи используется тот же ключ.

**16. Чем 401 отличается от 403?**
401 — «не вошёл» (нет/неверный токен). 403 — «вошёл, но нельзя» (нет роли Admin).
401 возвращает `OnChallenge`, 403 — сам endpoint через `Results.Problem`.

**17. Как проверяется администратор?**
В JWT есть claim `role`. В `Program.cs` настроен `RoleClaimType = "role"` и политика
`AdminOnly` (`RequireRole("Admin")`). Она навешана на POST/PUT/DELETE/upload:
гость получает 401, пользователь с ролью User — 403. `AuthorId` при создании
берётся из токена, но менять чужие записи админ может.

**18. Где правило 24 часов?**
`IsLocked(entity)` в `EventEndpoints`: `entity.StartsAt - DateTime.UtcNow < 24h`.
Вызывается в PUT, DELETE и загрузке постера; возвращает 400 с ProblemDetails.

**19. Почему 400, а не 409?**
В задании перечислены коды 200/201/204/400/401/403/404. 400 укладывается в список
и означает «запрос нарушает правило».

**20. Как работает пагинация?**
`CountAsync()` до `Skip/Take` даёт `total`; `Skip((page-1)*pageSize)` и `Take(pageSize)`
дают страницу. `TotalPages` вычисляется на клиенте/в DTO.

**21. Как защищена сортировка от SQL-инъекции?**
`sort` проверяется валидатором (`date|price`), а в запросе используется `switch` —
в SQL попадает только заранее написанная ветка. Пользовательская строка не
подставляется в SQL.

**22. Как загружается файл?**
`IFormFile` в multipart, `PosterStorage.SaveAsync`: размер ≤ 5 МБ, расширение, MIME,
сигнатура, имя `Guid.NewGuid():N + ext`, запись в `wwwroot/uploads/posters`.
Старые файлы удаляются, путь хранится в `PosterPath`.

**23. Как работает Angular-сигнал?**
`signal(value)` хранит значение. Чтение в шаблоне/`computed` подписывает на изменения.
`query.update(...)` создаёт новый объект, `httpResource` видит изменение и
перезапрашивает данные.

**24. Зачем `httpResource`, если есть `HttpClient`?**
`httpResource` автоматически делает запрос, следит за сигналами, отдаёт
`value/isLoading/error/statusCode/reload` и отменяет устаревший запрос. Для списка с
фильтрами это удобно. Для POST/PUT/DELETE используется обычный `HttpClient`.

**25. Как работает бронирование мест?**
Любой вошедший пользователь шлёт `POST /api/events/{id}/reservations` с числом
мест. Сервер проверяет, что событие в будущем, что сумма броней не превышает
`Seats`, и что у пользователя ещё нет брони на это событие (уникальный индекс).
Свободно = `Seats − SUM(Reservations.Seats)`. Бронь можно отменить через
`DELETE /api/reservations/{id}` (только свою), список — `GET /api/reservations/my`.

**26. Как работают интерсептор и guard?**
Интерсептор клонирует запрос и добавляет `Authorization: Bearer <token>` из
`TokenStorage`. Guard перед маршрутом проверяет `isAuthenticated()`; если нет —
возвращает `UrlTree` на `/login` с `returnUrl`.

---

## 30 строк, которые нужно уметь объяснить

1. **`Program.cs:31`** — `builder.Configuration["Jwt:Key"]` читает секрет из
   user-secrets/переменных окружения. В `appsettings.json` его нет.
2. **`Program.cs:46`** — `MapInboundClaims = false`: не переименовывать `sub` в
   `ClaimTypes.NameIdentifier`, чтобы читать claim как есть.
3. **`Program.cs:54`** — `IssuerSigningKey`: тем же ключом проверяется подпись,
   которой токен был подписан.
4. **`Program.cs:97`** — `db.Database.Migrate()`: применяет миграции и создаёт БД.
5. **`AppDbContext.cs:44–46`** — конвертер цены: `decimal` ↔ `long` в копейках.
6. **`AppDbContext.cs:49–52`** — конвертер даты: при чтении ставит `Kind=Utc`,
   поэтому в JSON появляется `Z`.
7. **`AppDbContext.cs:57`** — `DeleteBehavior.Restrict`: площадку с событиями удалить
   нельзя.
8. **`EventEndpoints.cs:88`** — `new EventQuery(...)`: собираем параметры в DTO для
   валидатора.
9. **`EventEndpoints.cs:95`** — `AsNoTracking()`: только чтение, EF не тратит память
   на отслеживание.
10. **`EventEndpoints.cs:118`** — `CountAsync` до пагинации: `total` для кнопок.
11. **`EventEndpoints.cs:121–129`** — `switch` по `sort`: в SQL идёт только выбранная
    ветка, инъекция невозможна.
12. **`EventEndpoints.cs:132–133`** — `Skip/Take`: пропуск предыдущих страниц и размер
    страницы.
13. **`EventEndpoints.cs:137`** — `SpecifyKind(..., Utc)`: гарантирует `Z` в JSON.
14. **`EventEndpoints.cs:168`** — `GetUserId()`: id из claim `sub`.
15. **`EventEndpoints.cs`** — `AuthorId = userId.Value`: автор из токена, не из тела.
16. **`Program.cs`** — `AddPolicy("AdminOnly", p => p.RequireRole("Admin"))`: 403 для
    обычного пользователя на все изменения.
17. **`EventEndpoints.cs`** — 400, если `IsLocked(entity)`.
18. **`EventEndpoints.cs:279–281`** — удаление записи, затем файла постера.
19. **`EventEndpoints.cs:316`** — `SaveAsync`: проверки файла и запись на диск.
20. **`EventEndpoints.cs:345`** — само правило: `StartsAt - UtcNow < 24h`.
21. **`ValidationFilter.cs:15`** — `OfType<T>()`: находит DTO среди аргументов
    endpoint'а.
22. **`ValidationFilter.cs:24`** — `Results.ValidationProblem(...)`: 400 + ProblemDetails
    + `errors`.
23. **`Pbkdf2PasswordHasher.cs:23–25`** — PBKDF2 и формат хранения хеша.
24. **`Pbkdf2PasswordHasher.cs:41`** — `FixedTimeEquals`: сравнение за постоянное
    время.
25. **`JwtTokenService.cs:33–41`** — сборка токена: claims, issuer/audience, срок,
    подпись HS256.
26. **`PosterStorage.cs:58`** — имя файла `Guid.NewGuid():N + расширение`.
27. **`auth.interceptor.ts:13–14`** — `request.clone({ setHeaders: ... })`: запросы
    неизменяемы, добавляем заголовок.
28. **`auth.guard.ts:14–15`** — `createUrlTree(['/login'], { queryParams: { returnUrl } })`:
    редирект с запоминанием адреса.
29. **`event.service.ts:14–15`** — `httpResource(() => { query(); return {...} })`:
    чтение сигнала делает ресурс реактивным.
30. **`event-list-page.ts:59–60`** — `query.update(value => ({ ...value, ...state, page: 1 }))`:
    новый объект → `httpResource` перезапрашивает список.

Дополнительно:

31. **`event-form-page.ts:118–125`** — сборка payload: trim, `null` для пустого
    описания, `fromDatetimeLocal` → UTC, `Number(...)`.
32. **`event-form-page.ts:203–210`** — `setServerErrors`: ошибки сервера кладутся в
    контролы как `{ server: "..." }`.
33. **`event-form-page.ts:82–96`** — `effect` заполняет форму, когда пришли данные
    редактирования.
34. **`event-details-page.ts:62–81`** — проверки файла на клиенте до отправки.
35. **`event-list-page.ts:30`** — `listResource(this.query)`: список сам обновляется
    при изменении `query`.

---

## Если спросят «покажи код»

Открывайте в таком порядке:

1. `backend/Afisha.Api/Program.cs` — как всё собирается.
2. `backend/Afisha.Api/Endpoints/EventEndpoints.cs` — CRUD, фильтры, 401/403/400.
3. `backend/Afisha.Api/Data/AppDbContext.cs` — связи, конвертеры, HasData.
4. `backend/Afisha.Api/Validation/ValidationFilter.cs` — валидация и ProblemDetails.
5. `frontend/src/app/core/event.service.ts` — `httpResource`.
6. `frontend/src/app/core/auth.interceptor.ts` и `auth.guard.ts` — токен и защита.
7. `frontend/src/app/pages/event-form-page/event-form-page.ts` — форма и серверные
   ошибки.

---

## Частые ошибки на защите (не допускать)

- Говорить «токен шифрует данные». JWT **подписан**, payload читается.
- Говорить «пароль хранится в токене». В токене только id/email/имя.
- Говорить «клиент проверяет роль, поэтому сервер не нужен». Сервер проверяет
  повторно — клиенту нельзя доверять.
- Путать 401 и 403.
- Говорить «EF выполняет SQL сразу». Нет: `IQueryable` ленив, SQL — на материализации.
- Утверждать, что `HasData` выполняется при каждом старте. Нет, это часть миграции.
- Забыть, что файл базы и `uploads` не коммитятся.
