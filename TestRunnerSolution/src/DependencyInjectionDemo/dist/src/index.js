"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const container_1 = require("./container");
const tokens_1 = require("./services/tokens");
const guidRunIdProvider_1 = require("./services/guidRunIdProvider");
const consoleTestResultNotifier_1 = require("./services/consoleTestResultNotifier");
const inMemoryTestResultNotifier_1 = require("./services/inMemoryTestResultNotifier");
const testRunReporter_1 = require("./testRunReporter");
console.log("=== Dependency Injection proof-of-concept ===\n");
// -----------------------------------------------------------------------
// PROOF 1: constructor injection + singleton lifetime
//
// TestRunReporter never does "new GuidRunIdProvider()" or
// "new ConsoleTestResultNotifier()" itself. The container builds those
// objects and *hands them in* through the constructor. Because both are
// registered as singletons, every resolve returns the exact same instance.
// -----------------------------------------------------------------------
console.log("--- Proof 1: container builds TestRunReporter's dependencies for it ---");
const container1 = new container_1.Container();
container1.register(tokens_1.RUN_ID_PROVIDER, () => new guidRunIdProvider_1.GuidRunIdProvider(), "singleton");
container1.register(tokens_1.TEST_RESULT_NOTIFIER, () => new consoleTestResultNotifier_1.ConsoleTestResultNotifier(), "singleton");
container1.register(testRunReporter_1.TEST_RUN_REPORTER, (c) => new testRunReporter_1.TestRunReporter(c.resolve(tokens_1.RUN_ID_PROVIDER), c.resolve(tokens_1.TEST_RESULT_NOTIFIER)), "transient");
const reporterA = container1.resolve(testRunReporter_1.TEST_RUN_REPORTER);
const reporterB = container1.resolve(testRunReporter_1.TEST_RUN_REPORTER);
console.log(`  TestRunReporter is transient -> two resolves give different instances: ${reporterA !== reporterB}`);
const runIdProviderA = container1.resolve(tokens_1.RUN_ID_PROVIDER);
const runIdProviderB = container1.resolve(tokens_1.RUN_ID_PROVIDER);
console.log(`  RunIdProvider is singleton  -> two resolves give the same instance: ${runIdProviderA === runIdProviderB}\n`);
reporterA.report("Login_ShouldSucceed_WithValidCredentials", true);
reporterB.report("Checkout_ShouldFail_WhenCartIsEmpty", false);
console.log();
// -----------------------------------------------------------------------
// PROOF 2: swap the injected implementation without touching TestRunReporter
//
// This is the real proof that DI works: TestRunReporter's source code is
// never edited. We just register a different TestResultNotifier in the
// container, and TestRunReporter's behaviour changes because the object
// it was handed changed.
// -----------------------------------------------------------------------
console.log("--- Proof 2: swapping the registered implementation changes behaviour ---");
const container2 = new container_1.Container();
container2.register(tokens_1.RUN_ID_PROVIDER, () => new guidRunIdProvider_1.GuidRunIdProvider(), "singleton");
container2.register(tokens_1.TEST_RESULT_NOTIFIER, () => new inMemoryTestResultNotifier_1.InMemoryTestResultNotifier(), "singleton"); // <- only line that changed
container2.register(testRunReporter_1.TEST_RUN_REPORTER, (c) => new testRunReporter_1.TestRunReporter(c.resolve(tokens_1.RUN_ID_PROVIDER), c.resolve(tokens_1.TEST_RESULT_NOTIFIER)), "transient");
const reporter2 = container2.resolve(testRunReporter_1.TEST_RUN_REPORTER);
reporter2.report("Search_ShouldReturnResults_ForKnownQuery", true);
reporter2.report("Search_ShouldReturnEmpty_ForUnknownQuery", true);
const inMemoryNotifier = container2.resolve(tokens_1.TEST_RESULT_NOTIFIER);
console.log("  TestRunReporter.report() was called with no code change, yet the notifier that");
console.log(`  received the calls is now InMemoryTestResultNotifier, which recorded ${inMemoryNotifier.received.length} result(s):`);
for (const { runId, testName, passed } of inMemoryNotifier.received) {
    console.log(`    run=${runId} passed=${passed} test=${testName}`);
}
console.log();
console.log("=== Proven: TestRunReporter depends on abstractions, the container decides");
console.log("    which concrete classes it receives, and lifetimes (singleton/transient)");
console.log("    are honoured. See tests/testRunReporter.test.ts for the same proof done");
console.log("    with zero DI container involved at all. ===");
