import 'dotenv/config';
import path from 'path';

const dbPath = process.env.TEST_RUNNER_DB_PATH
  ? path.resolve(process.env.TEST_RUNNER_DB_PATH)
  : path.resolve(__dirname, '../../../../data/test-runner.db');

export const config = {
  api: {
    baseUrl: process.env.BASE_URL ?? 'http://localhost:5111',
  },
  db: {
    database: dbPath,
  },
};
