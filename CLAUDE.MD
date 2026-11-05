# CoffeeShop: baseline .NET + Angular
Código en inglés. Respuestas en inglés, breves.

## Stack
.NET (última LTS), ASP.NET Core minimal APIs, EF Core + SQL Server, xUnit,
Angular (última estable, standalone + signals), Docker Compose, MinIO (S3-compatible).

## Estructura
/src/Domain /src/Application /src/Infrastructure /src/Api
/tests/*.Tests  /web (Angular)  /docker-compose.yml  /openapi.json

## Dominio
Product(name, origin, roast) -> Sku(grind: WholeBean|Ground, size_g: 500|1000)
EffectivePrice(sku_id, amount, currency, effective_from)  // solo INSERT
Customer, Order(status), OrderLine(unit_price_snapshot, price_id), Inventory(product_id, stock_g)

## Reglas
- Precio vigente(sku, asOf) = fila con mayor effective_from <= asOf. UNIQUE(sku_id, effective_from).
- OrderLine congela el precio al crear el pedido.
- Inventario en gramos por producto; cada línea descuenta size_g * qty.
- Outbox transaccional; consumidores idempotentes. Bus detrás de IMessageBus.
- Storage detrás de IFileStorage (MinIO ahora, S3 después).
- Roles: Admin y Customer; política AdminOnly (rol Admin) en los endpoints de administración.
- Precios solo INSERT, sin PUT ni DELETE: duplicado (sku_id, effective_from) -> 409 desde el índice único; effective_from anterior a hoy (UTC) -> 422.
- Tests de integración con Testcontainers.MsSql (requieren Docker) y un único fixture compartido.
- Outbox at-least-once: cada mensaje se escribe en la misma transacción que el cambio de estado y puede entregarse más de una vez.
- Handlers idempotentes vía ProcessedMessage (MessageId, HandlerName), insertado en la misma transacción que sus efectos en la base de datos.
- El dispatcher del outbox se puede alojar aparte: Outbox__Enabled=false lo saca de la API.

## Trabajo (ahorro de tokens)
- No expliques el código ni muestres archivos completos; resume cambios en máx. 5 líneas.
- No releas archivos que acabas de escribir; usa grep en lugar de listar directorios enteros.
- Build/test en modo silencioso (`dotnet build -v q`, `dotnet test --nologo -v q`); muestra solo fallos.
- Ante ambigüedad menor, decide y anótalo en DECISIONS.md (1 línea); no preguntes.
- Un milestone por vez. Commit convencional al terminar.
- Antes de cada push: code review (nivel high) de los commits sin publicar; corrige los hallazgos confirmados, vuelve a pasar los tests y commitea antes de publicar.
