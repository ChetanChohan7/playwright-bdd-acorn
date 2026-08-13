import type { TestResultNotifier } from "./types";

export interface RecordedResult {
  runId: string;
  testName: string;
  passed: boolean;
}

/**
 * Fake/test-double implementation — just records what it was told instead
 * of writing to the console. Used two ways in this project:
 *   1. In index.ts, swapped in for ConsoleTestResultNotifier to prove the
 *      container — not TestRunReporter — decides which class runs.
 *   2. In the tests, constructed directly (no container at all) to prove
 *      TestRunReporter is testable purely because it depends on the
 *      TestResultNotifier abstraction, not a concrete class.
 */
export class InMemoryTestResultNotifier implements TestResultNotifier {
  readonly received: RecordedResult[] = [];

  notify(runId: string, testName: string, passed: boolean): void {
    this.received.push({ runId, testName, passed });
  }
}
