# TestRunnerSolution

Proof-of-concept solution using a C# .NET TestDataService over SQLite and a TypeScript Playwright-BDD test suite.

## Structure
- `src/TestDataService`: .NET Web API exposing SQLite-backed test data.
- `src/UiTests`: TypeScript Playwright-BDD automation suite.
- `src/DependencyInjectionDemo`: minimal TypeScript project proving dependency injection works — see its [README](src/DependencyInjectionDemo/README.md).
- `data/test-runner.db`: Example SQLite database.
- `scripts/run-all.sh`: macOS/Linux helper to run the API and then the tests.

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

## macOS setup

-# 1. Start the API

    cd src/TestDataService
    dotnet run

-# 2. Install UI test dependencies
The service is configured to run on  http://localhost:5111 .
    cd src/UiTests
    npm install
    npx playwright install
    copy .env.example .env

-# 3. Run the tests
    npm test

-# 4. Option
    npm run test:headed
    npm run test:ui
    npm run test:debug
    npm run report

### Optional shortcut for macOS/Linux
After completing setup, you can run the API and tests together with:

```bash
./scripts/run-all.sh


## Windows setup

-# 1. Start the API

    cd src/TestDataService
    dotnet run

-# 2. Install UI test dependencies
The service is configured to run on  http://localhost:5111 .
    cd src/UiTests
    npm install
    npx playwright install
    cp .env.example .env

-# 3. Run the tests
    npm test

-# 4. Option
    npm run test:headed
    npm run test:ui
    npm run test:debug
    npm run report