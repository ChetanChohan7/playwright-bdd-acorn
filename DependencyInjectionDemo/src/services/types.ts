/**
 * The abstractions. TestRunReporter (the consumer, see ../testRunReporter.ts)
 * only ever knows about these interfaces — it never knows or cares which
 * concrete class is actually doing the work. That's the whole point of
 * dependency injection.
 */

/** Hands out the identifier for the current test run. Registered as a
 *  singleton so every class that depends on it during a single run
 *  receives the *same* id. */
export interface RunIdProvider {
  readonly runId: string;
}

/** "However we tell the outside world about a test result." */
export interface TestResultNotifier {
  notify(runId: string, testName: string, passed: boolean): void;
}
