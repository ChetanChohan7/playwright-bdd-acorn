import { expect } from '@playwright/test';
import { createBdd } from 'playwright-bdd';
import { getTableNames, getTableRows } from '../api/testDataClient';
import {
  TestPlanArraySchema,
  IncompatibleTestPlanArraySchema
} from '../contracts/testPlan.schema';
import {
  BugArraySchema,
  IncompatibleBugArraySchema
} from '../contracts/bugs.schema';
import { validateWithConciseLogging } from '../support/zod-logger';

const { Given, When, Then } = createBdd();

let tableNames: string[] = [];
let selectedTableName = '';
let responseRows: unknown = [];

When('I request the list of test data tables', async ({ request }) => {
  tableNames = await getTableNames(request);
});

Then('the test data tables response should contain at least one table', async () => {
  expect(tableNames.length).toBeGreaterThan(0);
});

Given('I know an available test data table', async ({ request }) => {
  tableNames = await getTableNames(request);
  expect(tableNames.length).toBeGreaterThan(0);
  selectedTableName = tableNames[0];
});

When('I request test data for that table', async ({ request }) => {
  responseRows = await getTableRows(request, selectedTableName, 5);
});

Then('the test data rows response should be successful', async () => {
  expect(Array.isArray(responseRows)).toBeTruthy();
});

Given('the test data API is available', async ({ request }) => {
  tableNames = await getTableNames(request);
  expect(tableNames.length).toBeGreaterThan(0);
});

When('I request rows from table {string}', async ({ request }, tableName: string) => {
  responseRows = await getTableRows(request, tableName, 25);
});

When('I request {int} row from table {string}', async ({ request }, limit: number, tableName: string) => {
  responseRows = await getTableRows(request, tableName, limit);
});

Then('the response matches the test plan schema', async () => {
  const parsed = validateWithConciseLogging(TestPlanArraySchema, responseRows, {
    label: 'TestPlanArraySchema',
    sampleLimit: 4,
  });

  expect(parsed.length).toBeGreaterThan(0);
});

Then('the response should fail the incompatible schema validation', async () => {
  const result = IncompatibleTestPlanArraySchema.safeParse(responseRows);

  if (result.success) {
    console.error('Unexpected success:', result.data);
  } else {
    const seen = new Set<string>();

    console.error('Expected schema failure:');

    for (const issue of result.error.issues) {
      const field = String(issue.path[1] ?? issue.path[0] ?? '(root)');
      const summary = `${field} -> ${issue.message}`;

      if (!seen.has(summary)) {
        seen.add(summary);
        console.error(`- ${summary}`);
      }
    }
  }

  expect(result.success).toBeFalsy();
});

Then('the response incorrectly matches the incompatible schema', async () => {
  const result = IncompatibleTestPlanArraySchema.safeParse(responseRows);

  if (!result.success) {
    const seen = new Set<string>();

    console.error('Intentional visible failure: incompatible schema mismatch');

    for (const issue of result.error.issues) {
      const field = String(issue.path[1] ?? issue.path[0] ?? '(root)');
      const summary = `${field} -> ${issue.message}`;

      if (!seen.has(summary)) {
        seen.add(summary);
        console.error(`- ${summary}`);
      }
    }
  }

  expect(result.success).toBeTruthy();
});

Then('the response matches the bug schema', async () => {
  const parsed = validateWithConciseLogging(BugArraySchema, responseRows, {
    label: 'BugArraySchema',
    sampleLimit: 1,
  });

  expect(parsed.length).toBe(1);
});

Then('the bug response should fail the incompatible schema validation', async () => {
  const result = IncompatibleBugArraySchema.safeParse(responseRows);

  if (result.success) {
    console.error('Unexpected success:', result.data);
  } else {
    const seen = new Set<string>();

    console.error('Expected bug schema failure:');

    for (const issue of result.error.issues) {
      const field = String(issue.path[1] ?? issue.path[0] ?? '(root)');
      const summary = `${field} -> ${issue.message}`;

      if (!seen.has(summary)) {
        seen.add(summary);
        console.error(`- ${summary}`);
      }
    }
  }

  expect(result.success).toBeFalsy();
});

Then('the bug response incorrectly matches the incompatible schema', async () => {
  const result = IncompatibleBugArraySchema.safeParse(responseRows);

  if (!result.success) {
    const seen = new Set<string>();

    console.error('Intentional visible failure: incompatible bug schema mismatch');

    for (const issue of result.error.issues) {
      const field = String(issue.path[1] ?? issue.path[0] ?? '(root)');
      const summary = `${field} -> ${issue.message}`;

      if (!seen.has(summary)) {
        seen.add(summary);
        console.error(`- ${summary}`);
      }
    }
  }

  expect(result.success).toBeTruthy();
});
