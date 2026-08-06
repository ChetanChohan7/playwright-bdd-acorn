import { getDbPool } from '../dbClient';

const SAFE_NAME = /^[A-Za-z0-9_]+$/;

export async function getTableNames(): Promise<string[]> {
  const db = await getDbPool();
  const rows = await db('sqlite_master')
    .select('name')
    .where('type', 'table')
    .whereNot('name', 'like', 'sqlite_%')
    .orderBy('name');

  return rows.map((row: { name: string }) => row.name);
}

export async function getRows(tableName: string, limit = 25): Promise<Record<string, unknown>[]> {
  if (!tableName || !SAFE_NAME.test(tableName)) {
    throw new Error('Invalid table name.');
  }

  if (limit < 1 || limit > 200) {
    throw new Error('Limit must be between 1 and 200.');
  }

  const db = await getDbPool();
  return db(tableName).select('*').limit(limit);
}
