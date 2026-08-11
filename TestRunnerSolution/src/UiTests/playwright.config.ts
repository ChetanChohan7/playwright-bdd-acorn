import 'dotenv/config';
import { defineConfig, devices } from '@playwright/test';
import { defineBddConfig } from 'playwright-bdd';

const testDir = defineBddConfig({
  paths: ['features/**/*.feature'],
  require: ['steps/**/*.ts', 'support/**/*.ts']
});

if (!process.env.BASE_URL) {
  throw new Error(
    'BASE_URL is not set. Point it to the UI under test, for example https://your-ui-app.local'
  );
}

export default defineConfig({
  testDir,
  timeout: 30_000,
  reporter: [['html', { open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
