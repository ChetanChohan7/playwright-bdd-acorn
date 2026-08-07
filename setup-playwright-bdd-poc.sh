#!/bin/zsh
set -euo pipefail

SOLUTION_NAME="${1:-TestRunnerSolution}"
ROOT_DIR="$(pwd)/${SOLUTION_NAME}"
API_DIR="$ROOT_DIR/src/TestDataService"
UI_DIR="$ROOT_DIR/src/UiTests"
DATA_DIR="$ROOT_DIR/data"
SCRIPTS_DIR="$ROOT_DIR/scripts"
DB_SOURCE_DEFAULT="$HOME/Downloads/test-runner.db"
DB_SOURCE="${2:-$DB_SOURCE_DEFAULT}"
API_PORT="${API_PORT:-5111}"

info() {
  printf '\n[%s] %s\n' "setup" "$1"
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Error: required command '$1' is not installed or not on PATH."
    exit 1
  fi
}

write_file() {
  local path="$1"
  /bin/mkdir -p "$(/usr/bin/dirname "$path")"
  /bin/cat > "$path"
}

require_cmd dotnet
require_cmd node
require_cmd npm
require_cmd python3

info "Creating root structure"
mkdir -p "$ROOT_DIR/src" "$DATA_DIR" "$SCRIPTS_DIR"

if [ -f "$DB_SOURCE" ]; then
  info "Copying example SQLite database from $DB_SOURCE"
  cp "$DB_SOURCE" "$DATA_DIR/test-runner.db"
else
  echo "Warning: database file not found at $DB_SOURCE"
  echo "The solution will still be created, but you will need to place test-runner.db into $DATA_DIR manually."
fi

cd "$ROOT_DIR"

if [ ! -f "$ROOT_DIR/${SOLUTION_NAME}.sln" ]; then
  info "Creating .NET solution"
  dotnet new sln -n "$SOLUTION_NAME"
fi

if [ ! -d "$API_DIR" ]; then
  info "Creating TestDataService Web API project"
  dotnet new webapi -n TestDataService -o "$API_DIR" --use-controllers
fi

cd "$API_DIR"
info "Installing .NET packages"
dotnet add package Microsoft.Data.Sqlite
dotnet add package Dapper
dotnet add package Swashbuckle.AspNetCore

info "Writing TestDataService files"
write_file "$API_DIR/appsettings.json" <<'JSON'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "TestRunner": "Data Source=../../data/test-runner.db"
  }
}
JSON

mkdir -p Controllers Contracts Data Repositories Services Models

write_file "$API_DIR/Contracts/TestDataRowDto.cs" <<'EOF2'
namespace TestDataService.Contracts;

public class TestDataRowDto
{
    public Dictionary<string, object?> Data { get; set; } = new();
}
EOF2

write_file "$API_DIR/Services/SqliteQueryService.cs" <<'EOF2'
using Dapper;
using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;

namespace TestDataService.Services;

public class SqliteQueryService
{
    private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
    private readonly IConfiguration _configuration;

    public SqliteQueryService(IConfiguration configuration)
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
EOF2

write_file "$API_DIR/Controllers/TestDataController.cs" <<'EOF2'
using Microsoft.AspNetCore.Mvc;
using TestDataService.Services;

namespace TestDataService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestDataController : ControllerBase
{
    private readonly SqliteQueryService _sqliteQueryService;

    public TestDataController(SqliteQueryService sqliteQueryService)
    {
        _sqliteQueryService = sqliteQueryService;
    }

    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        var tables = await _sqliteQueryService.GetTableNamesAsync();
        return Ok(tables);
    }

    [HttpGet("{tableName}")]
    public async Task<IActionResult> GetRows(string tableName, [FromQuery] int limit = 25)
    {
        try
        {
            var result = await _sqliteQueryService.GetRowsAsync(tableName, limit);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
EOF2

write_file "$API_DIR/Program.cs" <<'EOF2'
using TestDataService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SqliteQueryService>();
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
EOF2

cd "$ROOT_DIR"
dotnet sln "$ROOT_DIR/${SOLUTION_NAME}.sln" add "$API_DIR/TestDataService.csproj" >/dev/null 2>&1 || true

info "Creating UiTests project structure"
mkdir -p "$UI_DIR"

write_file "$UI_DIR/package.json" <<'JSON'
{
  "name": "ui-tests",
  "version": "1.0.0",
  "private": true,
  "type": "commonjs",
  "scripts": {
    "bddgen": "bddgen",
    "test": "npm run bddgen && playwright test",
    "test:headed": "npm run bddgen && playwright test --headed",
    "test:ui": "npm run bddgen && playwright test --ui",
    "test:debug": "npm run bddgen && playwright test --debug",
    "report": "playwright show-report"
  },
  "devDependencies": {
    "@playwright/test": "^1.48.0",
    "@types/node": "^22.0.0",
    "@types/pg": "^8.20.0",
    "axios": "^1.7.0",
    "dotenv": "^16.4.5",
    "pg": "^8.12.0",
    "playwright-bdd": "^8.2.0",
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
JSON

write_file "$UI_DIR/tsconfig.json" <<'JSON'
{
  "compilerOptions": {
    "target": "ES2020",
    "module": "commonjs",
    "moduleResolution": "node",
    "types": ["node", "@playwright/test"],
    "resolveJsonModule": true,
    "esModuleInterop": true,
    "strict": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "outDir": "dist"
  },
  "include": [
    "features/**/*.ts",
    "steps/**/*.ts",
    "api/**/*.ts",
    "clients/**/*.ts",
    "contracts/**/*.ts",
    "fixtures/**/*.ts",
    "support/**/*.ts",
    "utils/**/*.ts",
    "pages/**/*.ts",
    "playwright.config.ts"
  ]
}
JSON

write_file "$UI_DIR/playwright.config.ts" <<'EOF2'
import { defineConfig, devices } from '@playwright/test';
import { defineBddConfig } from 'playwright-bdd';

const testDir = defineBddConfig({
  paths: ['features/**/*.feature'],
  require: ['steps/**/*.ts', 'support/**/*.ts']
});

export default defineConfig({
  testDir,
  timeout: 30_000,
  reporter: [['html', { open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL ?? 'https://playwright.dev',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
EOF2

mkdir -p "$UI_DIR/features" "$UI_DIR/steps" "$UI_DIR/api" "$UI_DIR/clients" "$UI_DIR/contracts" "$UI_DIR/fixtures" "$UI_DIR/support" "$UI_DIR/utils" "$UI_DIR/pages"

write_file "$UI_DIR/contracts/testData.ts" <<'EOF2'
export interface TestDataRow {
  [key: string]: string | number | boolean | null;
}
EOF2

write_file "$UI_DIR/api/testDataClient.ts" <<'EOF2'
import type { APIRequestContext } from '@playwright/test';
import type { TestDataRow } from '../contracts/testData';

export async function getTableNames(request: APIRequestContext): Promise<string[]> {
  const response = await request.get(`${process.env.TEST_DATA_SERVICE_URL ?? 'http://localhost:5111'}/api/TestData/tables`);
  if (!response.ok()) {
    throw new Error(`Failed to fetch table names: ${response.status()} ${response.statusText()}`);
  }
  return (await response.json()) as string[];
}

export async function getTableRows(request: APIRequestContext, tableName: string, limit = 25): Promise<TestDataRow[]> {
  const baseUrl = process.env.TEST_DATA_SERVICE_URL ?? 'http://localhost:5111';
  const response = await request.get(`${baseUrl}/api/TestData/${tableName}?limit=${limit}`);
  if (!response.ok()) {
    throw new Error(`Failed to fetch test data for table ${tableName}: ${response.status()} ${response.statusText()}`);
  }
  return (await response.json()) as TestDataRow[];
}
EOF2

write_file "$UI_DIR/features/test-data-api.feature" <<'EOF2'
Feature: Consume test data through the API
  As a UI automation engineer
  I want to fetch test data from the TestDataService
  So that my UI and API checks stay decoupled from direct database access

  Scenario: List available test data tables
    When I request the list of test data tables
    Then the test data tables response should contain at least one table

  Scenario: Read rows from a table
    Given I know an available test data table
    When I request test data for that table
    Then the test data rows response should be successful
EOF2

write_file "$UI_DIR/steps/testData.steps.ts" <<'EOF2'
import { expect } from '@playwright/test';
import { createBdd } from 'playwright-bdd';
import { getTableNames, getTableRows } from '../api/testDataClient';

const { Given, When, Then } = createBdd();

let tableNames: string[] = [];
let selectedTableName = '';
let responseRows: unknown[] = [];

When('I request the list of test data tables', async ({ request }) => {
  tableNames = await getTableNames(request);
});

Then('the test data tables response should contain at least one table', async () => {
  expect(tableNames.length).toBeGreaterThan(0);
});

Given('I know an available test data table', async ({ request }) => {
  tableNames = await getTableNames(request);
  expect(tableNames.length).toBeGreaterThan(0);
  selectedTableName = tableNames[0];
});

When('I request test data for that table', async ({ request }) => {
  responseRows = await getTableRows(request, selectedTableName, 5);
});

Then('the test data rows response should be successful', async () => {
  expect(Array.isArray(responseRows)).toBeTruthy();
});
EOF2

write_file "$UI_DIR/support/env.ts" <<'EOF2'
import dotenv from 'dotenv';

dotenv.config();
EOF2

write_file "$UI_DIR/.env.example" <<EOF2
BASE_URL=https://playwright.dev
TEST_DATA_SERVICE_URL=http://localhost:${API_PORT}
EOF2

write_file "$UI_DIR/.gitignore" <<'EOF2'
node_modules/
playwright-report/
test-results/
.env
EOF2

cd "$UI_DIR"
info "Installing npm packages"
npm install

info "Installing Playwright browsers"
npx playwright install

cd "$ROOT_DIR"
write_file "$ROOT_DIR/README.md" <<EOF2
# ${SOLUTION_NAME}

Proof-of-concept solution using a C# .NET TestDataService over SQLite and a TypeScript Playwright-BDD test suite.

## Structure
- src/TestDataService: .NET Web API exposing SQLite-backed test data
- src/UiTests: TypeScript Playwright-BDD automation suite
- data/test-runner.db: example SQLite database
- scripts/run-all.sh: helper to run API then tests

## Start the API
cd src/TestDataService
dotnet run

## Run the tests
cd src/UiTests
cp .env.example .env
npm test
EOF2

write_file "$SCRIPTS_DIR/run-all.sh" <<EOF2
#!/bin/zsh
set -euo pipefail

ROOT_DIR="\$(cd "\$(dirname "$0")/.." && pwd)"
API_URL="http://localhost:${API_PORT}"

(cd "\$ROOT_DIR/src/TestDataService" && dotnet run) &
API_PID=\$!

cleanup() {
  kill "\$API_PID" 2>/dev/null || true
}
trap cleanup EXIT

printf 'Waiting for TestDataService to start'
for _ in {1..20}; do
  if curl -sf "\$API_URL/health" >/dev/null 2>&1; then
    break
  fi
  printf '.'
  sleep 1
done
printf '\n'

(cd "\$ROOT_DIR/src/UiTests" && cp -n .env.example .env && npm test)
EOF2
chmod +x "$SCRIPTS_DIR/run-all.sh"

info "Bootstrap complete"
printf 'Solution created at: %s\n' "$ROOT_DIR"
printf 'API project: %s\n' "$API_DIR"
printf 'UI project: %s\n' "$UI_DIR"
if [ -f "$DATA_DIR/test-runner.db" ]; then
  printf 'Database copied to: %s\n' "$DATA_DIR/test-runner.db"
else
  printf 'Database not copied automatically. Place your file at: %s\n' "$DATA_DIR/test-runner.db"
fi
printf '\nNext steps:\n'
printf '  cd %s/src/TestDataService && dotnet run\n' "$ROOT_DIR"
printf '  cd %s/src/UiTests && cp .env.example .env && npm test\n' "$ROOT_DIR"
printf '  Or run: %s/scripts/run-all.sh\n' "$ROOT_DIR"