// src/UiTests/steps/testData.steps.ts
import { expect, type APIRequestContext } from '@playwright/test';
import { createBdd } from 'playwright-bdd';
import { TestDataClient } from '../clients/testDataClient';
import { getRows, getRowById } from '../db/repositories/testDataRepository';
import {
  TestPlanArraySchema,
  IncompatibleTestPlanArraySchema
} from '../contracts/testPlan.schema';
import {
  BugArraySchema,
  IncompatibleBugArraySchema
} from '../contracts/bugs.schema';
import { validateWithConciseLogging } from '../support/zod-logger';
import { normaliseRows } from '../support/normalisers/testData.normaliser';

const { Given, When, Then } = createBdd();

type TestDataWorld = {
  tableNames?: string[];
  tableName?: string;
  limit?: number;
  requestedId?: string;
  responseStatus?: number;
  apiResponseBody?: unknown;
};

type StepFixtures = {
  request: APIRequestContext;
};

async function getAvailableTableNames(request: APIRequestContext): Promise<string[]> {
  const client = new TestDataClient(request);
  const response = await client.getTables();

  expect(response.ok()).toBeTruthy();

  const body = await response.json();
  expect(Array.isArray(body)).toBeTruthy();

  return body as string[];
}

Then(
  'the response status should be {int}',
  async function (this: TestDataWorld, {}: StepFixtures, statusCode: number) {
    expect(this.responseStatus).toBe(statusCode);
  }
);

When(
  'I request the list of test data tables',
  async function (this: TestDataWorld, { request }: StepFixtures) {
    this.tableNames = await getAvailableTableNames(request);
  }
);

Then(
  'the test data tables response should contain at least one table',
  async function (this: TestDataWorld) {
    expect(this.tableNames?.length ?? 0).toBeGreaterThan(0);
  }
);

Given(
  'I know an available test data table',
  async function (this: TestDataWorld, { request }: StepFixtures) {
    this.tableNames = await getAvailableTableNames(request);
    expect(this.tableNames.length).toBeGreaterThan(0);

    this.tableName = this.tableNames[0];
  }
);

Given(
  'the test data API is available',
  async function (this: TestDataWorld, { request }: StepFixtures) {
    this.tableNames = await getAvailableTableNames(request);
    expect(this.tableNames.length).toBeGreaterThan(0);
  }
);

When(
  'I request test data for that table',
  async function (this: TestDataWorld, { request }: StepFixtures) {
    expect(this.tableName).toBeTruthy();

    this.limit = 5;
    this.requestedId = undefined;

    const client = new TestDataClient(request);
    const response = await client.getRows(this.tableName!, this.limit);

    this.responseStatus = response.status();
    this.apiResponseBody = await response.json();
  }
);

When(
  'I request rows from table {string}',
  async function (this: TestDataWorld, { request }: StepFixtures, tableName: string) {
    this.tableName = tableName;
    this.limit = 25;
    this.requestedId = undefined;

    const client = new TestDataClient(request);
    const response = await client.getRows(tableName, this.limit);

    this.responseStatus = response.status();
    this.apiResponseBody = await response.json();
  }
);

When(
  'I request {int} row from table {string}',
  async function (
    this: TestDataWorld,
    { request }: StepFixtures,
    limit: number,
    tableName: string
  ) {
    this.tableName = tableName;
    this.limit = limit;
    this.requestedId = undefined;

    const client = new TestDataClient(request);
    const response = await client.getRows(tableName, limit);

    this.responseStatus = response.status();
    this.apiResponseBody = await response.json();
  }
);

When(
  'I request {int} rows from table {string}',
  async function (
    this: TestDataWorld,
    { request }: StepFixtures,
    limit: number,
    tableName: string
  ) {
    this.tableName = tableName;
    this.limit = limit;
    this.requestedId = undefined;

    const client = new TestDataClient(request);
    const response = await client.getRows(tableName, limit);

    this.responseStatus = response.status();
    this.apiResponseBody = await response.json();
  }
);

When(
  'I request row {string} from table {string}',
  async function (
    this: TestDataWorld,
    { request }: StepFixtures,
    id: string,
    tableName: string
  ) {
    this.tableName = tableName;
    this.requestedId = id;
    this.limit = undefined;

    const client = new TestDataClient(request);
    const response = await client.getRowById(tableName, id);

    this.responseStatus = response.status();
    this.apiResponseBody = await response.json();
  }
);

Then(
  'the test data rows response should be successful',
  async function (this: TestDataWorld) {
    expect(this.responseStatus).toBe(200);
    expect(Array.isArray(this.apiResponseBody)).toBeTruthy();
  }
);

Then(
  'the API response should match the database',
  async function (this: TestDataWorld) {
    expect(this.tableName).toBeTruthy();

    let dbResult: unknown;

    if (this.requestedId !== undefined) {
      dbResult = await getRowById(this.tableName!, this.requestedId);
    } else {
      dbResult = await getRows(this.tableName!, this.limit ?? 1);
    }

    expect(normaliseRows(this.apiResponseBody)).toEqual(normaliseRows(dbResult));
  }
);

Then(
  'the response matches the test plan schema',
  async function (this: TestDataWorld) {
    const parsed = validateWithConciseLogging(TestPlanArraySchema, this.apiResponseBody, {
      label: 'TestPlanArraySchema',
      sampleLimit: 4,
    });

    expect(parsed.length).toBeGreaterThan(0);
  }
);

Then(
  'the response should fail the incompatible schema validation',
  async function (this: TestDataWorld) {
    const result = IncompatibleTestPlanArraySchema.safeParse(this.apiResponseBody);

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
  }
);

Then(
  'the response incorrectly matches the incompatible schema',
  async function (this: TestDataWorld) {
    const result = IncompatibleTestPlanArraySchema.safeParse(this.apiResponseBody);

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
  }
);

Then(
  'the response matches the bug schema',
  async function (this: TestDataWorld) {
    const parsed = validateWithConciseLogging(BugArraySchema, this.apiResponseBody, {
      label: 'BugArraySchema',
      sampleLimit: 1,
    });

    expect(parsed.length).toBe(1);
  }
);

Then(
  'the bug response should fail the incompatible schema validation',
  async function (this: TestDataWorld) {
    const result = IncompatibleBugArraySchema.safeParse(this.apiResponseBody);

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
  }
);

Then(
  'the bug response incorrectly matches the incompatible schema',
  async function (this: TestDataWorld) {
    const result = IncompatibleBugArraySchema.safeParse(this.apiResponseBody);

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
  }
);
