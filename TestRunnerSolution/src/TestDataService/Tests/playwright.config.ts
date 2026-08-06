import { defineConfig } from '@playwright/test';
import 'dotenv/config';

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5111',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'ui',
      testMatch: ['UiTests/**/*.spec.ts'],
    },
    {
      name: 'api',
      testMatch: ['ApiTests/**/*.spec.ts'],
    },
  ],
});
