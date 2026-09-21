"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const node_test_1 = require("node:test");
const strict_1 = __importDefault(require("node:assert/strict"));
const node_crypto_1 = require("node:crypto");
const testRunReporter_1 = require("../src/testRunReporter");
const inMemoryTestResultNotifier_1 = require("../src/services/inMemoryTestResultNotifier");
/**
 * A third implementation of RunIdProvider, written only for tests, that
 * GuidRunIdProvider and TestRunReporter's own code know nothing about.
 * Being able to write and inject this — without touching any production
 * class — is the payoff of coding against the RunIdProvider abstraction.
 */
class FixedRunIdProvider {
    constructor(runId) {
        this.runId = runId;
    }
}
// These tests deliberately never touch the DI container (no Container,
// no .register()/.resolve()). They just do "new TestRunReporter(fakeA, fakeB)",
// the same way you'd new up any object.
//
// That this is even possible is the proof: TestRunReporter's constructor
// asks for RunIdProvider and TestResultNotifier — interfaces — not
// GuidRunIdProvider or ConsoleTestResultNotifier. So a test can hand it
// cheap, deterministic fakes instead of the real console-writing
// implementation, with zero changes to TestRunReporter itself.
(0, node_test_1.test)("report() passes the runId from the injected provider to the injected notifier", () => {
    const runId = "11111111-1111-1111-1111-111111111111";
    const runIdProvider = new FixedRunIdProvider(runId);
    const notifier = new inMemoryTestResultNotifier_1.InMemoryTestResultNotifier();
    const reporter = new testRunReporter_1.TestRunReporter(runIdProvider, notifier);
    reporter.report("Sample_Test", true);
    strict_1.default.equal(notifier.received.length, 1);
    strict_1.default.deepEqual(notifier.received[0], { runId, testName: "Sample_Test", passed: true });
});
(0, node_test_1.test)("report() uses whichever notifier was injected, not a hardcoded one", () => {
    const runIdProvider = new FixedRunIdProvider((0, node_crypto_1.randomUUID)());
    const notifierA = new inMemoryTestResultNotifier_1.InMemoryTestResultNotifier();
    const notifierB = new inMemoryTestResultNotifier_1.InMemoryTestResultNotifier();
    // Same TestRunReporter class, two different injected notifiers.
    const reporterWithA = new testRunReporter_1.TestRunReporter(runIdProvider, notifierA);
    const reporterWithB = new testRunReporter_1.TestRunReporter(runIdProvider, notifierB);
    reporterWithA.report("Only_For_A", true);
    reporterWithB.report("Only_For_B", false);
    strict_1.default.equal(notifierA.received.length, 1);
    strict_1.default.equal(notifierA.received[0].testName, "Only_For_A");
    strict_1.default.equal(notifierB.received.length, 1);
    strict_1.default.equal(notifierB.received[0].testName, "Only_For_B");
});
(0, node_test_1.test)("report() called multiple times forwards each result in order", () => {
    const runIdProvider = new FixedRunIdProvider((0, node_crypto_1.randomUUID)());
    const notifier = new inMemoryTestResultNotifier_1.InMemoryTestResultNotifier();
    const reporter = new testRunReporter_1.TestRunReporter(runIdProvider, notifier);
    reporter.report("First", true);
    reporter.report("Second", false);
    reporter.report("Third", true);
    strict_1.default.deepEqual(notifier.received.map((r) => r.testName), ["First", "Second", "Third"]);
    strict_1.default.deepEqual(notifier.received.map((r) => r.passed), [true, false, true]);
});
