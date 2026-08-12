import type { TestResultNotifier } from "./types";

/**
 * "Real" implementation — writes the result to the console, formatted like
 * a CI log line. This is what a production entry point would wire up.
 */
export class ConsoleTestResultNotifier implements TestResultNotifier {
  notify(runId: string, testName: string, passed: boolean): void {
    const status = passed ? "PASS" : "FAIL";
    console.log(`  [console-notifier] run=${runId} ${status} ${testName}`);
  }
}
