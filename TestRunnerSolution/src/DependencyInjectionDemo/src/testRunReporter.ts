import { createToken } from "./container";
import type { RunIdProvider, TestResultNotifier } from "./services/types";

/**
 * The "consumer" in this demo. Its constructor asks for two abstractions
 * only (RunIdProvider and TestResultNotifier), injected by whoever creates
 * it. It has zero knowledge of GuidRunIdProvider, ConsoleTestResultNotifier
 * or InMemoryTestResultNotifier — whoever builds this object decides which
 * concrete classes it gets.
 */
export class TestRunReporter {
  constructor(
    private readonly runIdProvider: RunIdProvider,
    private readonly notifier: TestResultNotifier
  ) {}

  report(testName: string, passed: boolean): void {
    this.notifier.notify(this.runIdProvider.runId, testName, passed);
  }
}

export const TEST_RUN_REPORTER = createToken<TestRunReporter>("TestRunReporter");
