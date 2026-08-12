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

-# 1. Start the API (keep this terminal running)

    cd src/TestDataService
    dotnet run

-# 2. In a **new terminal**, starting again from `TestRunnerSolution/`, build the DependencyInjectionDemo solution (once, or after changing it)

    cd ../DependencyInjectionDemo
    npm install
    npm run build

-# 3. Install UI test dependencies (continuing in the same terminal as step 2)

The service is configured to run on http://localhost:5111.

    cd ../TestRunnerSolution/src/UiTests
    npm install
    npx playwright install
    copy .env.example .env

-# 4. Run the tests — see [Running the tests](#running-the-tests) below for what each command does

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

-# 1. Start the API (keep this terminal running)

    cd src/TestDataService
    dotnet run

-# 2. In a **new terminal**, starting again from `TestRunnerSolution/`, build the DependencyInjectionDemo solution (once, or after changing it)

    cd ../DependencyInjectionDemo
    npm install
    npm run build

-# 3. Install UI test dependencies (continuing in the same terminal as step 2)

The service is configured to run on http://localhost:5111.

    cd ../TestRunnerSolution/src/UiTests
    npm install
    npx playwright install
    cp .env.example .env

-# 4. Run the tests — see [Running the tests](#running-the-tests) below for what each command does

    npm test

-# 5. Optional

    npm run test:headed
    npm run test:ui
    npm run test:debug
    npm run report
    npm run verify-di

## Running the tests

There are three independent test/verification commands in this repo. `npm test` (Playwright-BDD) is the main one; the other two exist to prove the dependency-injection example works.

| Command | Run from | What it does |
|---|---|---|
| `npm test` | `src/UiTests` | Generates step definitions (`bddgen`) and runs the Playwright-BDD suite against the running `TestDataService` API (`http://localhost:5111`). Needs step 1 running first. |
| `npm run verify-di` | `src/UiTests` | Imports `Container`/`TestRunReporter` from the separate `DependencyInjectionDemo` solution (installed as a local dependency), wires its own container, and proves DI still works across the solution boundary. No API needed. Requires `DependencyInjectionDemo` to have been built first (setup step 2). See its [README](../DependencyInjectionDemo/README.md#proof-3-a-separate-solution-consumes-this-one-and-still-injects-its-own-dependency). |
| `npm test` | `../DependencyInjectionDemo` | Runs that solution's own unit tests (Node's built-in `node:test` runner) — constructs `TestRunReporter` with hand-written fakes, no container involved. See its [README](../DependencyInjectionDemo/README.md#running-the-tests-di-without-a-container-at-all). |

Each block below is independent (uses a subshell) and assumes you start from `TestRunnerSolution/` — safe to paste and run in order:

```bash
# Playwright-BDD suite (needs TestDataService running, see setup step 1)
(cd src/UiTests && npm test)

# DI proof — TestRunnerSolution calling into the separate solution
(cd src/UiTests && npm run verify-di)

# DI demo's own unit tests, run from the separate solution
(cd ../DependencyInjectionDemo && npm test)
```
