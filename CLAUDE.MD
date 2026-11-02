# CoffeeShop: baseline .NET + Angular
Código en inglés. Respuestas en español, breves.

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

## Trabajo (ahorro de tokens)
- No expliques el código ni muestres archivos completos; resume cambios en máx. 5 líneas.
- No releas archivos que acabas de escribir; usa grep en lugar de listar directorios enteros.
- Build/test en modo silencioso (`dotnet build -v q`, `dotnet test --nologo -v q`); muestra solo fallos.
- Ante ambigüedad menor, decide y anótalo en DECISIONS.md (1 línea); no preguntes.
- Un milestone por vez. Commit convencional al terminar.