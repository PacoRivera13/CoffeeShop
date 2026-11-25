# Roastery web (Angular)

```bash
npm start          # ng serve on http://localhost:4200, /api proxied to the API on :5121
npm test           # coverage check + Vitest component specs in Chromium
npm run e2e        # Playwright page specs, desktop and mobile, API mocked
npm run e2e:full   # @full purchase flow against `docker compose up` (E2E_BASE_URL, default http://localhost:8080;
                   # E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD = the stack's Admin__Email / Admin__Password)
npm run gen:api    # regenerate src/app/api from ../openapi.json
```

**Design system.** "Roastery" is Angular Material 3 with cream surfaces, Fraunces headings and Inter body text, all
self-hosted. The theme lives in `src/styles.scss` (`mat.theme` plus surface overrides), the palettes in
`src/styles/_theme-colors.scss` and the breakpoints in `src/styles/_breakpoints.scss`; components use only the
`--mat-sys-*` tokens, never raw colors, and are mobile-first with 44px touch targets on compact screens.
