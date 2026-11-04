# CoffeeShop
Coffee shop backend: catalog and effective pricing on .NET 10 minimal APIs, EF Core and SQL Server.

- Test: `dotnet test --nologo -v q` (Docker must be running)
- Database (LocalDB): `dotnet tool restore`, then `dotnet ef database update -p src/Infrastructure -s src/Api`
- Run: set `Jwt__Key` (32+ chars), `Jwt__Issuer` and `Jwt__Audience` with env vars or `dotnet user-secrets`, then `dotnet run --project src/Api`
- Endpoints: `POST /auth/register`, `POST /auth/login`, `GET /auth/me`, `GET /products`, `GET /skus/{id}/price?asOf=2026-06-01T00:00:00Z`; Swagger UI at `/swagger` in Development
- Docker: copy `.env.example` to `.env`, set a strong password, then `docker compose up -d` and set `ConnectionStrings__Default`.

`docker-compose.yml` has not been run yet; see `DECISIONS.md`.
