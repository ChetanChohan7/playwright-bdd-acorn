import type { APIRequestContext } from '@playwright/test';

export async function getTableNames(request: APIRequestContext): Promise<string[]> {
  const response = await request.get(`${process.env.TEST_DATA_SERVICE_URL ?? 'http://localhost:5111'}/api/TestData/tables`);
  if (!response.ok()) {
    throw new Error(`Failed to fetch table names: ${response.status()} ${response.statusText()}`);
  }
  return (await response.json()) as string[];
}

export async function getTableRows<T = unknown>(
  request: APIRequestContext,
  tableName: string,
  limit = 25
): Promise<T[]> {
  const baseUrl = process.env.TEST_DATA_SERVICE_URL ?? 'http://localhost:5111';
  const response = await request.get(`${baseUrl}/api/TestData/${tableName}?limit=${limit}`);
  if (!response.ok()) {
    throw new Error(`Failed to fetch test data for table ${tableName}: ${response.status()} ${response.statusText()}`);
  }
  return (await response.json()) as T[];
}
