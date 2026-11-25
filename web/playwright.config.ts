import { defineConfig, devices } from '@playwright/test';

const isCI = !!process.env['CI'];
// `npm run e2e:full` sets E2E_FULL: it runs only the @full specs against a running docker compose stack.
const isFull = !!process.env['E2E_FULL'];

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'desktop', testIgnore: /full\//, use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile', testIgnore: /full\//, use: { ...devices['Pixel 7'] } },
    {
      name: 'full',
      testDir: './e2e/full',
      grep: /@full/,
      use: { ...devices['Desktop Chrome'], baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:8080' },
    },
  ],
  // The default run mocks the API and serves the app with ng serve; the full run uses the compose stack instead.
  ...(isFull
    ? {}
    : {
        webServer: {
          command: 'npm start',
          url: 'http://localhost:4200',
          reuseExistingServer: !isCI,
          timeout: 180_000,
        },
      }),
});
