import { test, expect } from '@playwright/test';
import { getTableNames } from '../../TestSupport/db/repositories/testDataRepository';

test('test data tables endpoint responds', async ({ request }) => {
  const response = await request.get('/api/TestData/tables');
  expect(response.ok()).toBeTruthy();
});

test('database can be queried through shared knex support', async () => {
  const tables = await getTableNames();
  expect(Array.isArray(tables)).toBeTruthy();
});
