# CoffeeShop
Coffee shop backend: catalog and effective pricing on .NET 10 minimal APIs, EF Core and SQL Server.

- Test: `dotnet test --nologo -v q` (Docker must be running)
- Database (LocalDB): `dotnet tool restore`, then `dotnet ef database update -p src/Infrastructure -s src/Api`
- Run: set `Jwt__Key` (32+ chars), `Jwt__Issuer` and `Jwt__Audience` with env vars or `dotnet user-secrets`, then `dotnet run --project src/Api`
- Endpoints: auth, catalog, admin prices and orders; see `openapi.json` or Swagger UI at `/swagger` in Development
- Docker: copy `.env.example` to `.env` and fill it; `docker compose up -d sqlserver minio`; migrate with `ConnectionStrings__Default="Server=127.0.0.1,1433;Database=CoffeeShop;User Id=sa;Password=..."` and the Database command above; then `docker compose up -d api` (http://localhost:8080)

MinIO console: http://localhost:9001. Invoices are stored as `invoices/{orderId}.pdf`; see `DECISIONS.md`.
