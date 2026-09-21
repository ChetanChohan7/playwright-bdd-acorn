"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ConsoleTestResultNotifier = void 0;
/**
 * "Real" implementation — writes the result to the console, formatted like
 * a CI log line. This is what a production entry point would wire up.
 */
class ConsoleTestResultNotifier {
    notify(runId, testName, passed) {
        const status = passed ? "PASS" : "FAIL";
        console.log(`  [console-notifier] run=${runId} ${status} ${testName}`);
    }
}
exports.ConsoleTestResultNotifier = ConsoleTestResultNotifier;
