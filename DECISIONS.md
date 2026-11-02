# Decisions
- Local verification uses LocalDB because Docker is not installed; `docker-compose.yml` has not been run.
- IDs are `int` identity columns, with fixed values in the seed.
- `asOf` accepts ISO 8601 only and is read as UTC when it has no offset; it defaults to the current time.
- A SKU with no effective price returns 404 with its own title, distinct from an unknown SKU.
- Prices are insert-only in `SaveChanges` and in a database trigger that rejects UPDATE and DELETE.
- Inventory is not seeded in this milestone; it was not part of the requested seed.
- Seed prices are in EUR and start on 2026-01-01; SKU 1 has extra rows to show price history and a future change.
- Migrations are applied explicitly with `dotnet ef`, never at startup.
- Pinned System.Security.Cryptography.Pkcs 10.0.14 directly: the 9.0.11 pulled in by EF Core SqlServer has known CVEs.
