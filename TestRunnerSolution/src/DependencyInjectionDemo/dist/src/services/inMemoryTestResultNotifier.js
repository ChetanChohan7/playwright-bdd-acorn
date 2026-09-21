"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.InMemoryTestResultNotifier = void 0;
/**
 * Fake/test-double implementation — just records what it was told instead
 * of writing to the console. Used two ways in this project:
 *   1. In index.ts, swapped in for ConsoleTestResultNotifier to prove the
 *      container — not TestRunReporter — decides which class runs.
 *   2. In the tests, constructed directly (no container at all) to prove
 *      TestRunReporter is testable purely because it depends on the
 *      TestResultNotifier abstraction, not a concrete class.
 */
class InMemoryTestResultNotifier {
    constructor() {
        this.received = [];
    }
    notify(runId, testName, passed) {
        this.received.push({ runId, testName, passed });
    }
}
exports.InMemoryTestResultNotifier = InMemoryTestResultNotifier;
