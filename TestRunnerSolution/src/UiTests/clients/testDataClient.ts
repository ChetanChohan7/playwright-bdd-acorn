import type { APIRequestContext, APIResponse } from '@playwright/test';

export class TestDataClient {
  private readonly testDataServiceUrl: string;

  constructor(private readonly request: APIRequestContext) {
    this.testDataServiceUrl = process.env.TEST_DATA_SERVICE_URL ?? 'http://localhost:5111';
  }

  async getTables(): Promise<APIResponse> {
    return this.request.get(`${this.testDataServiceUrl}/api/TestData/tables`);
  }

  async getRows(tableName: string, limit = 25): Promise<APIResponse> {
    return this.request.get(`${this.testDataServiceUrl}/api/TestData/${tableName}?limit=${limit}`);
  }

  async getRowById(tableName: string, id: string | number): Promise<APIResponse> {
    return this.request.get(`${this.testDataServiceUrl}/api/TestData/${tableName}?limit=1&id=${id}`);
    // or adjust if you later add a dedicated /{tableName}/{id} route
  }
}
