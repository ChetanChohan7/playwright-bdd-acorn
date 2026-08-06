import { test, expect } from '@playwright/test';

test('health endpoint is reachable from UI project', async ({ request }) => {
  const response = await request.get('/health');
  expect(response.ok()).toBeTruthy();
});
