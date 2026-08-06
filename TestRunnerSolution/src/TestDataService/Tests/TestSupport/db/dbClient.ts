import knex, { Knex } from 'knex';
import { config } from '../config/config';

let db: Knex | undefined;

export async function getDbPool(): Promise<Knex> {
  if (!db) {
    db = knex({
      client: 'sqlite3',
      connection: {
        filename: config.db.database,
      },
      useNullAsDefault: true,
    });
  }

  return db;
}

export async function closeDbPool(): Promise<void> {
  if (db) {
    await db.destroy();
    db = undefined;
  }
}
