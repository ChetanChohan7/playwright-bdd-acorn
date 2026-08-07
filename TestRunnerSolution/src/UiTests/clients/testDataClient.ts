import type { APIRequestContext, APIResponse } from '@playwright/test';

export class TestDataClient {
  constructor(private readonly request: APIRequestContext) {}

  async getTables(): Promise<APIResponse> {
    return this.request.get('/api/TestData/tables');
  }

  async getRows(tableName: string, limit = 25): Promise<APIResponse> {
    return this.request.get(`/api/TestData/${tableName}?limit=${limit}`);
  }

  async getRowById(tableName: string, id: string | number): Promise<APIResponse> {
    return this.request.get(`/api/TestData/${tableName}?limit=1&id=${id}`);
    // or adjust if you later add a dedicated /{tableName}/{id} route
  }
}