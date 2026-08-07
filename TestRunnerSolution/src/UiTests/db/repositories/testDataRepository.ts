// src/UiTests/db/repositories/testDataRepository.ts
import { db } from '../dbClient';

export async function getRows(tableName: string, limit = 1) {
  return db(tableName).select('*').limit(limit);
}

export async function getRowById(tableName: string, id: string | number) {
  return db(tableName).where({ id }).first();
}
