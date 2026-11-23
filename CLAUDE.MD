# CoffeeShop: baseline .NET + Angular
Code in English. Answers in English, brief.

## Stack
.NET (latest LTS), ASP.NET Core minimal APIs, EF Core + SQL Server, xUnit,
Angular (latest stable, standalone + signals), Docker Compose, MinIO (S3-compatible).

## Structure
/src/Domain /src/Application /src/Infrastructure /src/Api
/tests/*.Tests  /web (Angular)  /docker-compose.yml  /openapi.json

## Domain
Product(name, origin, roast) -> Sku(grind: WholeBean|Ground, size_g: 500|1000)
EffectivePrice(sku_id, amount, currency, effective_from)  // INSERT only
Customer, Order(status), OrderLine(unit_price_snapshot, price_id), Inventory(product_id, stock_g)
InventoryMovement(product_id, delta_g, reason: Reserve|Release|Restock, order_id?, created_at)  // INSERT only; stock_g = sum of delta_g
Order.idempotency_key: same Idempotency-Key from the same customer -> original order (200), without reserving stock or writing to the outbox

## Rules
- Current price(sku, asOf) = the row with the greatest effective_from <= asOf. UNIQUE(sku_id, effective_from).
- OrderLine freezes the price when the order is created.
- Inventory in grams per product; each line deducts size_g * qty.
- Transactional outbox; idempotent consumers. Bus behind IMessageBus.
- Storage behind IFileStorage (MinIO now, S3 later).
- Roles: Admin and Customer; AdminOnly policy (Admin role) on the admin endpoints.
- Prices are INSERT only, no PUT or DELETE: duplicate (sku_id, effective_from) -> 409 from the unique index; effective_from before today (UTC) -> 422.
- Integration tests with Testcontainers (SQL Server, MinIO; Docker required) and a single shared fixture.
- At-least-once outbox: each message is written in the same transaction as the state change and may be delivered more than once.
- Handlers are idempotent via ProcessedMessage (MessageId, HandlerName), inserted in the same transaction as their database side effects.
- The outbox dispatcher can be hosted separately: Outbox__Enabled=false removes it from the API.
- Handlers with external I/O (e.g. uploads) run outside a transaction (IRunsOutsideTransaction) and are idempotent through a deterministic key + unique constraint.
- All file access goes through IFileStorage; object keys are deterministic (invoices/{orderId}.pdf) and invoice generation is idempotent.
- Download URLs are presigned and short-lived (5 minutes) and are signed for the public endpoint; API responses never expose the bucket, storage key fields or the internal storage endpoint.

## Work (token saving)
- Do not explain the code or show whole files; summarize changes in at most 5 lines.
- Do not re-read files you just wrote; use grep instead of listing whole directories.
- Build/test in quiet mode (`dotnet build -v q`, `dotnet test --nologo -v q`); show only failures.
- For minor ambiguity, decide and record it in DECISIONS.md (1 line); do not ask.
- One milestone at a time. Conventional commit when done.
- Before each push: high-effort code review of the unpushed commits; fix confirmed findings, rerun the tests and commit before publishing.
