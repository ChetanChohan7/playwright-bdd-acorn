#!/bin/zsh
set -euo pipefail

ROOT="${1:-/Users/chetanchohan/Projects/inspired-poc/TestRunnerSolution/src/TestDataService}"
TESTS_ROOT="$ROOT/Tests"
UI_DIR="$TESTS_ROOT/UiTests"
API_DIR="$TESTS_ROOT/ApiTests"
SHARED_DIR="$TESTS_ROOT/TestSupport"
DB_DIR="$SHARED_DIR/db"
CONFIG_DIR="$SHARED_DIR/config"
DATA_DIR="$ROOT/../../data"
DB_FILE="$DATA_DIR/test-runner.db"
SERVICE_FILE="$ROOT/Services/SqliteQueryService.cs"
CONTROLLER_FILE="$ROOT/Controllers/TestDataController.cs"
PROGRAM_FILE="$ROOT/Program.cs"
CSPROJ_FILE="$ROOT/TestDataService.csproj"
PACKAGE_JSON="$TESTS_ROOT/package.json"
TSCONFIG_FILE="$TESTS_ROOT/tsconfig.json"
ENV_FILE="$TESTS_ROOT/.env"
PLAYWRIGHT_CONFIG="$TESTS_ROOT/playwright.config.ts"
UI_SPEC_DIR="$UI_DIR/specs"
API_SPEC_DIR="$API_DIR/specs"

require_file() {
  local file="$1"
  if [[ ! -f "$file" ]]; then
    echo "Required file not found: $file" >&2
    exit 1
  fi
}

backup_file() {
  local file="$1"
  if [[ -f "$file" ]]; then
    cp "$file" "$file.bak"
  fi
}

require_file "$SERVICE_FILE"
require_file "$CONTROLLER_FILE"
require_file "$PROGRAM_FILE"
require_file "$CSPROJ_FILE"

mkdir -p "$UI_SPEC_DIR" "$API_SPEC_DIR" "$DB_DIR/repositories" "$CONFIG_DIR" "$DATA_DIR"

backup_file "$SERVICE_FILE"
backup_file "$CONTROLLER_FILE"
backup_file "$PROGRAM_FILE"
backup_file "$CSPROJ_FILE"

cat > "$SERVICE_FILE" <<'EOF'
using Dapper;
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace TestDataService.Services;

public class TestRunnerQueryService
{
    private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
    private readonly IConfiguration _configuration;

    public TestRunnerQueryService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IEnumerable<IDictionary<string, object?>>> GetRowsAsync(string tableName, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(tableName) || !SafeName.IsMatch(tableName))
        {
            throw new ArgumentException("Invalid table name.");
        }

        if (limit < 1 || limit > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 200.");
        }

        var connectionString = _configuration.GetConnectionString("TestRunner");
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = $"SELECT * FROM [{tableName}] LIMIT {limit};";
        var rows = await connection.QueryAsync(sql);
        return rows.Select(row => (IDictionary<string, object?>)row);
    }

    public async Task<IEnumerable<string>> GetTableNamesAsync()
    {
        var connectionString = _configuration.GetConnectionString("TestRunner");
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        return await connection.QueryAsync<string>(sql);
    }
}
EOF

cat > "$CONTROLLER_FILE" <<'EOF'
using Microsoft.AspNetCore.Mvc;
using TestDataService.Services;

namespace TestDataService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestDataController : ControllerBase
{
    private readonly TestRunnerQueryService _testRunnerQueryService;

    public TestDataController(TestRunnerQueryService testRunnerQueryService)
    {
        _testRunnerQueryService = testRunnerQueryService;
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        var tables = await _testRunnerQueryService.GetTableNamesAsync();
        return Ok(tables);
    }

    [HttpGet("{tableName}")]
    public async Task<IActionResult> GetRows(string tableName, [FromQuery] int limit = 25)
    {
        try
        {
            var result = await _testRunnerQueryService.GetRowsAsync(tableName, limit);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
EOF

cat > "$PROGRAM_FILE" <<'EOF'
using TestDataService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TestRunnerQueryService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run("http://localhost:5111");
EOF

python3 - <<'PY' "$CSPROJ_FILE"
from pathlib import Path
import sys
path = Path(sys.argv[1])
text = path.read_text()
if '<None Remove="Tests/**" />' not in text:
    insert = '''
  <ItemGroup>
    <None Remove="Tests/**" />
  </ItemGroup>
'''
    text = text.replace('</Project>', insert + '\n</Project>')
path.write_text(text)
PY

cat > "$PACKAGE_JSON" <<'EOF'
{
  "name": "testrunnersolution-tests",
  "version": "1.0.0",
  "private": true,
  "type": "commonjs",
  "scripts": {
    "test": "playwright test",
    "test:ui": "playwright test --project=ui",
    "test:api": "playwright test --project=api",
    "test:headed": "playwright test --headed",
    "test:debug": "playwright test --debug",
    "report": "playwright show-report"
  },
  "devDependencies": {
    "@playwright/test": "^1.62.1",
    "@types/node": "^22.0.0",
    "@types/pg": "^8.20.0",
    "axios": "^1.7.0",
    "dotenv": "^16.4.5",
    "pg": "^8.12.0",
    "ts-node": "^10.9.2",
    "typescript": "^5.6.0"
  },
  "dependencies": {
    "knex": "^3.3.0",
    "sqlite": "^5.1.1",
    "sqlite3": "^6.0.1",
    "zod": "^4.4.3"
  }
}
EOF

cat > "$TSCONFIG_FILE" <<'EOF'
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "CommonJS",
    "moduleResolution": "Node",
    "strict": true,
    "esModuleInterop": true,
    "resolveJsonModule": true,
    "skipLibCheck": true,
    "types": ["node", "@playwright/test"]
  },
  "include": [
    "UiTests/**/*.ts",
    "ApiTests/**/*.ts",
    "TestSupport/**/*.ts",
    "playwright.config.ts"
  ]
}
EOF

cat > "$ENV_FILE" <<EOF
TEST_RUNNER_DB_PATH=$DB_FILE
BASE_URL=http://localhost:5111
EOF

cat > "$PLAYWRIGHT_CONFIG" <<'EOF'
import { defineConfig } from '@playwright/test';
import 'dotenv/config';

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5111',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'ui',
      testMatch: ['UiTests/**/*.spec.ts'],
    },
    {
      name: 'api',
      testMatch: ['ApiTests/**/*.spec.ts'],
    },
  ],
});
EOF

cat > "$CONFIG_DIR/config.ts" <<'EOF'
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
EOF

cat > "$DB_DIR/dbClient.ts" <<'EOF'
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
EOF

cat > "$DB_DIR/repositories/testDataRepository.ts" <<'EOF'
import { getDbPool } from '../dbClient';

const SAFE_NAME = /^[A-Za-z0-9_]+$/;

export async function getTableNames(): Promise<string[]> {
  const db = await getDbPool();
  const rows = await db('sqlite_master')
    .select('name')
    .where('type', 'table')
    .whereNotLike('name', 'sqlite_%')
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
EOF

cat > "$UI_SPEC_DIR/health.ui.spec.ts" <<'EOF'
import { test, expect } from '@playwright/test';

test('health endpoint is reachable from UI project', async ({ request }) => {
  const response = await request.get('/health');
  expect(response.ok()).toBeTruthy();
});
EOF

cat > "$API_SPEC_DIR/test-data.api.spec.ts" <<'EOF'
import { test, expect } from '@playwright/test';
import { getTableNames } from '../../TestSupport/db/repositories/testDataRepository';

test('test data tables endpoint responds', async ({ request }) => {
  const response = await request.get('/api/TestData/tables');
  expect(response.ok()).toBeTruthy();
});

test('database can be queried through shared knex support', async () => {
  const tables = await getTableNames();
  expect(Array.isArray(tables)).toBeTruthy();
});
EOF

cat > "$TESTS_ROOT/README.md" <<'EOF'
# TestDataService tests

This folder contains a shared Node-based Playwright test setup.

## Structure

- `UiTests/` contains UI-focused specs.
- `ApiTests/` contains API-focused specs.
- `TestSupport/` contains shared config and Knex DB utilities.

## Commands

- `npm install`
- `npm run test`
- `npm run test:ui`
- `npm run test:api`
EOF

echo "Done. Tests root created under: $TESTS_ROOT"
echo "Backups created with .bak suffix for modified C# files and .csproj."
