# Known issues

Deliberate deferrals found during the 6c verification pass. Each entry: symptom, cause, why it is deferred.

## Duplicate product names in the local catalogue

- **Symptom:** a dev database lists 9 products, two of them named "Yirgacheffe" (one Ethiopian demo product with stock,
  one legacy product without stock). Cards, inventory rows and their accessible names ("Manage Yirgacheffe") cannot be
  told apart by name.
- **Cause:** the migrations seed 3 products through `HasData` (used by the integration tests) and the 6a demo seed adds
  6 more; product names are not unique.
- **Why deferred:** keeping the migration seed was decided in 6a so the test data stays untouched. The full-stack spec
  now locates products by position instead of by name. A real fix (unique names, or moving the test catalogue out of the
  migrations) is a data-model decision for a later milestone.

## Debugger breakpoints not verified

- **Symptom:** the VS Code "Full stack" compound has been run only as separate tasks; F5 with breakpoints in the API and in
  the Angular TypeScript source has not been confirmed.
- **Cause:** the debugger cannot be driven from the automated tooling.
- **Why deferred:** postponed by the project owner until the projects are opened in each IDE. If Chrome breakpoints do
  not bind, check `webRoot` in `.vscode/launch.json` first.

## api.http not run by an automated runner

- **Symptom:** `api.http` was verified by replaying the same requests with a script, not by executing the file.
- **Cause:** its request-variable syntax (`{{name.response.body.$.x}}`) is the VS Code REST Client / Visual Studio
  syntax; the httpyac CLI does not support it.
- **Why deferred:** both target editors support the syntax; switching to a CLI-friendly format would break them.

## Outbox messages scheduled under a dev clock offset wait after it is removed

- **Symptom:** after running with `Dev__ClockOffsetDays` and then restarting without it, an outbox message that was still
  Pending or retrying is not dispatched until its `nextAttemptAt` (days ahead) is reached; e.g. an invoice never appears.
- **Cause:** outbox timestamps, backoff and claim leases use the application clock, which was shifted when they were written.
- **Why deferred:** the offset is a Development-only tool; rewriting timestamps on startup would add risk to production code
  for a dev convenience. Workaround: let pending messages drain before removing the offset, or run `scripts/dev-reset`.
