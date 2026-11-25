# Manual test plan

Checklist for a hands-on pass over the customer and admin flows against the local stack. Fill in **Result** (Pass / Fail
plus a note) as you go; failures belong in [known-issues.md](known-issues.md) or a new issue.

## Before you start

- Fresh data: `scripts/dev-reset.ps1` (or `.sh`), then `scripts/dev.ps1` (or `.sh`). It prints the URLs and the demo
  credentials from `.env`: Admin (`Admin__Email`) and demo customer (`Seed__CustomerEmail`).
- Web http://localhost:4200, API http://localhost:5121 (Swagger at `/swagger`), MinIO console http://localhost:9001.
- Rows run in order: later rows reuse state from earlier ones (C5 → C9 → C10, A2 → A4, A5 → A6).
- The catalogue lists **9 products**: the 6 demo products plus 3 that the migrations seed (Yirgacheffe, Huila,
  Mandheling, without stock). Price and stock checks below use the **demo** products; the demo Yirgacheffe is the one
  from Ethiopia listed after Huehuetenango.
- Demo prices (MXN): Yirgacheffe 259 (500 g) / 489 (1 kg) until today + 7 days, then 284.90 / 537.90. Stock: Yirgacheffe
  1500 g, Cerrado 400 g.
- API checks can use [api.http](../api.http) (VS Code REST Client or Visual Studio) or curl with a token from `POST /auth/login`.
- Every API response carries `X-Correlation-Id`; the same id prefixes the API's console log lines for that request.

## Checklist

| ID | Role | Steps | Expected | Result |
|---|---|---|---|---|
| C1 | Anonymous | Open http://localhost:4200 without signing in. Look at every card; on the demo Yirgacheffe pick each grind/size. | The catalogue loads without a sign-in redirect and lists 9 products with current prices. Demo Yirgacheffe shows the pre-increase prices (259 / 489 MXN). Cards offer "Sign in to buy" instead of "Add to cart". | |
| C2 | Customer | Create an account at /register; sign out; sign in again. Then sign out and try the same email with a wrong password. | Registration lands on the shop signed in; sign out returns to /login; sign in works. A wrong password shows an error message and stays on /login. | |
| C3 | Customer | On a card, switch grind (Whole bean / Ground) and size (500 g / 1000 g) several times quickly. | The price follows the selected SKU; while it loads a progress bar shows, never the previous SKU's price. | |
| C4 | Customer | Add two different SKUs. In the cart, set a quantity to 1, to 50, then try 0 and 51; remove one line; reload the page. | Quantity stays within 1–50 (the stepper stops at both bounds). The removed line is gone; after the reload the remaining line and quantity are still there. | |
| C5 | Customer | Check out a cart with a demo Mexican coffee (e.g. Chiapas Altura). | The order detail shows each line with the unit price at ordering time ("Prices were fixed when the order was placed"), the total, and status Pending. | |
| C6 | Customer | Put 2 × demo Yirgacheffe 1 kg in the cart (2000 g; stock is 1500 g) and check out. | Checkout fails with a stock message naming the product, 2000 g needed and 1500 g available (API 409). The cart keeps its contents. | |
| C7 | Customer | Put any Cerrado SKU in the cart (smallest is 500 g; stock is 400 g) and check out. | Stock message shows 400 g available (API 409); no order is created. | |
| C8 | Customer | With a cart ready, open DevTools → Network → Offline, press checkout, then go back Online and press checkout again. | The retry reuses the same `Idempotency-Key` (visible in the request headers) and exactly one new order appears in /orders. | |
| C9 | Customer | Open the Pending order from C5, pay it, wait a few seconds on the order page, then open the invoice. | Status becomes Paid; the invoice section shows a wait state first (API 202), then a download link. The PDF opens and its footer reads "Demo receipt - not fiscally valid". | |
| C10 | Customer + Admin | Cancel the order paid in C9. As Admin, open Inventory and the ledger of that product. Then, as the demo customer, open the seeded Delivered order. | The cancelled order shows Cancelled; the ledger has a Release entry with the same grams as the order's Reserve entry. The Delivered order offers no cancel action (and `POST /orders/{id}/cancel` returns 409). | |
| C11 | Customer | Signed in as a customer, open /admin/prices and /admin/inventory by URL. With the customer token call `POST /prices` and `GET /admin/inventory`. | Both pages redirect to the shop. Both API calls return 403. | |
| C12 | Customer | DevTools device toolbar at 360 px wide: browse, open the menu, add to cart, open /orders. | Navigation is in a drawer; a sticky cart bar appears once the cart has items; orders show as cards, not a table; nothing scrolls horizontally. | |
| C13 | Any | Toggle dark mode in the header, reload the page. | The theme stays as chosen after the reload. | |
| A1 | Admin | Sign in as Admin. Open the admin menu; then browse the shop, add to cart and open /orders. | The admin menu (Prices, Inventory) is visible. Customer pages still render for the Admin. | |
| A2 | Admin | Prices: for a demo SKU (e.g. Chiapas Altura WB 500 g) add a price effective tomorrow (UTC). Check the catalogue and the SKU's history. | The catalogue still shows today's price. History lists the new price first and does not mark it as current. | |
| A3 | Admin | Add a price with a date before today (UTC); then add the A2 price again (same SKU and date). | The past date shows a 422 message about the date; the duplicate shows a 409 message about an existing price for that date. | |
| A4 | Admin | Stop the stack, set `Dev__ClockOffsetDays=2` in the shell (or add it to `.vscode/api.env`), start it again; check the catalogue and history of the A2 SKU. Remove the setting afterwards, and only once /admin/outbox?status=Pending is empty (see known-issues.md). | The API log starts with a `DEV CLOCK SHIFTED BY +2 DAYS` warning. The A2 price is now current in the catalogue and marked current in history. Demo Yirgacheffe still shows 259 / 489. | |
| A5 | Admin | Inventory: restock the demo Yirgacheffe by 10000 g. Open its ledger. | Stock goes from 1500 g to 11500 g and the low-stock chip disappears. The ledger's newest entry is Restock +10000 g. | |
| A6 | Admin | For one product, add up every delta in its ledger (all pages). | The sum equals the stock figure shown in Inventory. | |
| A7 | Admin | Open `GET /admin/outbox` (api.http). Then `docker compose stop minio`, pay a new Pending order, check `/admin/outbox?status=Pending`; `docker compose start minio`, wait, check again and open the invoice. | Earlier flows appear as Done. With MinIO stopped the OrderPaid message stays Pending with attempts > 0, a later nextAttemptAt and a lastError; `/health` reports storage Unhealthy (503). After MinIO is back the message becomes Done and the invoice downloads. | |
| A8 | Admin | With the admin token call `GET /orders/{id}` for a customer's order. As a second customer, look through /orders and the order pages, and try the first customer's order id in the URL. | The Admin gets 200 for any order. The second customer only ever sees their own orders; another customer's order id shows "not found" (API 404). | |
