# TestRunnerSolution

Proof-of-concept solution using a C# .NET TestDataService over SQLite and a TypeScript Playwright-BDD test suite.

## Structure
- `src/TestDataService`: .NET Web API exposing SQLite-backed test data.
- `src/UiTests`: TypeScript Playwright-BDD automation suite. Also depends on the separate `DependencyInjectionDemo` solution (see below) as a local npm package — `src/UiTests/support/dependencyInjectionDemo.ts` (`npm run verify-di`) proves DI still works when the classes are consumed across solution boundaries.
- `data/test-runner.db`: Example SQLite database.
- `scripts/run-all.sh`: macOS/Linux helper to run the API and then the tests.

TestRunnerSolution is not the only solution in this repo — `../DependencyInjectionDemo` (a sibling of this folder) is a separate, independent TypeScript solution. It's built on its own and pulled into `UiTests` as an ordinary local dependency; see its [README](../DependencyInjectionDemo/README.md) for what it proves and how to build it.

## Prerequisites

Before running the solution on any operating system, install:
- .NET SDK
- Node.js and npm
- Playwright browser binaries

The API runs on:
- `http://localhost:5111`

The UI tests use environment variables from `.env` in `src/UiTests`.

### Clone the repository
```bash
git clone <repository-url>
cd TestRunnerSolution
```

## macOS setup

-# 1. Start the API

    cd src/TestDataService
    dotnet run

-# 2. Build the DependencyInjectionDemo solution (once, or after changing it)

    cd ../DependencyInjectionDemo
    npm install
    npm run build

-# 3. Install UI test dependencies

The service is configured to run on http://localhost:5111.

    cd ../TestRunnerSolution/src/UiTests
    npm install
    npx playwright install
    copy .env.example .env

-# 4. Run the tests

    npm test

-# 5. Optional

    npm run test:headed
    npm run test:ui
    npm run test:debug
    npm run report
    npm run verify-di

### Optional shortcut for macOS/Linux
After completing setup, you can run the API and tests together with:

```bash
./scripts/run-all.sh
```

## Windows setup

-# 1. Start the API

    cd src/TestDataService
    dotnet run

-# 2. Build the DependencyInjectionDemo solution (once, or after changing it)

    cd ../DependencyInjectionDemo
    npm install
    npm run build

-# 3. Install UI test dependencies

The service is configured to run on http://localhost:5111.

    cd ../TestRunnerSolution/src/UiTests
    npm install
    npx playwright install
    cp .env.example .env

-# 4. Run the tests

    npm test

-# 5. Optional

    npm run test:headed
    npm run test:ui
    npm run test:debug
    npm run report
    npm run verify-di
