"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.TEST_RUN_REPORTER = exports.TestRunReporter = void 0;
const container_1 = require("./container");
/**
 * The "consumer" in this demo. Its constructor asks for two abstractions
 * only (RunIdProvider and TestResultNotifier), injected by whoever creates
 * it. It has zero knowledge of GuidRunIdProvider, ConsoleTestResultNotifier
 * or InMemoryTestResultNotifier — whoever builds this object decides which
 * concrete classes it gets.
 */
class TestRunReporter {
    constructor(runIdProvider, notifier) {
        this.runIdProvider = runIdProvider;
        this.notifier = notifier;
    }
    report(testName, passed) {
        this.notifier.notify(this.runIdProvider.runId, testName, passed);
    }
}
exports.TestRunReporter = TestRunReporter;
exports.TEST_RUN_REPORTER = (0, container_1.createToken)("TestRunReporter");
