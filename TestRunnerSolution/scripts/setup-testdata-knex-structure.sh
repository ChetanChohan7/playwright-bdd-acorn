#!/bin/zsh
set -euo pipefail

ROOT="${1:-/Users/chetanchohan/Projects/inspired-poc/TestRunnerSolution/src/TestDataService}"
UI_DIR="$ROOT/UiTests"
API_DIR="$ROOT/ApiTests"
SHARED_DIR="$ROOT/TestSupport"
DB_DIR="$SHARED_DIR/db"
CONFIG_DIR="$SHARED_DIR/config"
DATA_DIR="$ROOT/../../data"
DB_FILE="$DATA_DIR/test-runner.db"
SERVICE_FILE="$ROOT/Services/SqliteQueryService.cs"
CONTROLLER_FILE="$ROOT/Controllers/TestDataController.cs"
PROGRAM_FILE="$ROOT/Program.cs"
CSPROJ_FILE="$ROOT/TestDataService.csproj"
UI_PACKAGE_JSON="$UI_DIR/package.json"
TSCONFIG_FILE="$ROOT/tsconfig.json"
ENV_FILE="$ROOT/.env"

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

mkdir -p "$UI_DIR/src/db/repositories" "$UI_DIR/src/support" "$API_DIR" "$DB_DIR" "$CONFIG_DIR" "$DATA_DIR"

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
if '<None Remove="UiTests/**" />' not in text:
    insert = '''
  <ItemGroup>
    <None Remove="UiTests/**" />
    <None Remove="ApiTests/**" />
    <None Remove="TestSupport/**" />
  </ItemGroup>
'''
    text = text.replace('</Project>', insert + '\n</Project>')
path.write_text(text)
PY

cat > "$UI_PACKAGE_JSON" <<'EOF'
{
  "name": "testrunnersolution-tests",
  "version": "1.0.0",
  "private": true,
  "type": "commonjs",
  "scripts": {
    "bddgen": "bddgen",
    "test": "npm run bddgen && playwright test",
    "test:ui": "npm run bddgen && playwright test --project=ui",
    "test:api": "npm run bddgen && playwright test --project=api",
    "test:headed": "npm run bddgen && playwright test --headed",
    "test:debug": "npm run bddgen && playwright test --debug",
    "report": "playwright show-report"
  },
  "devDependencies": {
    "@playwright/test": "^1.62.1",
    "@types/node": "^22.0.0",
    "@types/pg": "^8.20.0",
    "axios": "^1.7.0",
    "dotenv": "^16.4.5",
    "pg": "^8.12.0",
    "playwright-bdd": "^9.2.0",
    "ts-node": "^10.9.2",
    "typescript": "^5.6.0"
  },
  "dependencies": {
    "fast-xml-parser": "^5.10.1",
    "fast-xml-validator": "^1.4.0",
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
    "types": ["node"]
  },
  "include": [
    "UiTests/**/*.ts",
    "ApiTests/**/*.ts",
    "TestSupport/**/*.ts"
  ]
}
EOF

cat > "$ENV_FILE" <<EOF
TEST_RUNNER_DB_PATH=$DB_FILE
BASE_URL=http://localhost:5111
EOF

cat > "$CONFIG_DIR/config.ts" <<'EOF'
import 'dotenv/config';
import path from 'path';

const dbPath = process.env.TEST_RUNNER_DB_PATH
  ? path.resolve(process.env.TEST_RUNNER_DB_PATH)
  : path.resolve(__dirname, '../../../data/test-runner.db');

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

cat > "$UI_DIR/README.md" <<'EOF'
# TestDataService test structure

This folder hosts the Node-based test tooling for both UI and API tests.

## Layout

- `UiTests/` contains the shared Node project root and test runner scripts.
- `ApiTests/` is reserved for API-focused specs and support code.
- `TestSupport/` contains shared configuration and Knex database helpers.

## Next steps

1. Run `npm install` inside `UiTests`.
2. Add your Playwright config and test specs under `UiTests` and `ApiTests`.
3. Import shared DB helpers from `../TestSupport/db/...`.
EOF

echo "Done. Files created/updated under: $ROOT"
echo "Backups created with .bak suffix for modified C# files and .csproj."
