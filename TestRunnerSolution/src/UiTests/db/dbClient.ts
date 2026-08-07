// src/UiTests/db/dbClient.ts
import knex, { type Knex } from 'knex';

export const db: Knex = knex({
  client: process.env.DB_CLIENT || 'sqlite3',
  connection:
    process.env.DB_CLIENT === 'pg'
      ? {
          host: process.env.DB_HOST,
          port: Number(process.env.DB_PORT || 5432),
          user: process.env.DB_USER,
          password: process.env.DB_PASSWORD,
          database: process.env.DB_NAME,
        }
      : {
          filename: process.env.DB_FILE || './data/test-runner.db',
        },
  useNullAsDefault: true,
});
