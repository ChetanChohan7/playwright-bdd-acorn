/**
 * Public API of this package. This is the file external consumers import —
 * e.g. TestRunnerSolution's UiTests project depends on this package via a
 * local `file:` reference and imports from here (see
 * TestRunnerSolution/src/UiTests/support/dependencyInjectionDemo.ts).
 *
 * Nothing in this file is proof-specific: it's the same container, tokens,
 * abstractions, implementations and consumer that src/index.ts uses to print
 * its own standalone proof. Exporting them is what lets a *separate*
 * solution wire them into its own container and get the same guarantees.
 */

export { Container, createToken } from "./container";
export type { Lifetime, Token } from "./container";

export type { RunIdProvider, TestResultNotifier } from "./services/types";
export { RUN_ID_PROVIDER, TEST_RESULT_NOTIFIER } from "./services/tokens";

export { GuidRunIdProvider } from "./services/guidRunIdProvider";
export { ConsoleTestResultNotifier } from "./services/consoleTestResultNotifier";
export { InMemoryTestResultNotifier } from "./services/inMemoryTestResultNotifier";
export type { RecordedResult } from "./services/inMemoryTestResultNotifier";

export { TestRunReporter, TEST_RUN_REPORTER } from "./testRunReporter";
