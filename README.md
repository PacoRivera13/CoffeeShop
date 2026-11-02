# CoffeeShop
Coffee shop backend: catalog and effective pricing on .NET 10 minimal APIs, EF Core and SQL Server.

- Test: `dotnet test --nologo -v q`
- Database (LocalDB): `dotnet tool restore`, then `dotnet ef database update -p src/Infrastructure -s src/Api`
- Run: `dotnet run --project src/Api`
- Endpoints: `GET /products`, `GET /skus/{id}/price?asOf=2026-06-01T00:00:00Z`; Swagger UI at `/swagger` in Development
- Docker: copy `.env.example` to `.env`, set a strong password, then `docker compose up -d` and set `ConnectionStrings__Default`.

The Docker setup has not been run yet; only LocalDB has been verified. See `DECISIONS.md`.
