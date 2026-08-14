import 'dotenv/config';
import { defineConfig } from '@playwright/test';
import { defineBddConfig } from 'playwright-bdd';

const testDir = defineBddConfig({
  paths: ['features/**/*.feature'],
  require: ['steps/**/*.ts', 'support/**/*.ts'],
});

export default defineConfig({
  testDir,
  timeout: 150_000, // SMS send + API polling can take up to ~2.5 minutes
  fullyParallel: false, // shares one live phone number; scenarios must run serially
  reporter: [['html', { open: 'never' }]],
});
