import { Container } from "./container";
import { RUN_ID_PROVIDER, TEST_RESULT_NOTIFIER } from "./services/tokens";
import { GuidRunIdProvider } from "./services/guidRunIdProvider";
import { ConsoleTestResultNotifier } from "./services/consoleTestResultNotifier";
import { InMemoryTestResultNotifier } from "./services/inMemoryTestResultNotifier";
import { TestRunReporter, TEST_RUN_REPORTER } from "./testRunReporter";

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

const container1 = new Container();
container1.register(RUN_ID_PROVIDER, () => new GuidRunIdProvider(), "singleton");
container1.register(TEST_RESULT_NOTIFIER, () => new ConsoleTestResultNotifier(), "singleton");
container1.register(
  TEST_RUN_REPORTER,
  (c) => new TestRunReporter(c.resolve(RUN_ID_PROVIDER), c.resolve(TEST_RESULT_NOTIFIER)),
  "transient"
);

const reporterA = container1.resolve(TEST_RUN_REPORTER);
const reporterB = container1.resolve(TEST_RUN_REPORTER);
console.log(`  TestRunReporter is transient -> two resolves give different instances: ${reporterA !== reporterB}`);

const runIdProviderA = container1.resolve(RUN_ID_PROVIDER);
const runIdProviderB = container1.resolve(RUN_ID_PROVIDER);
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

const container2 = new Container();
container2.register(RUN_ID_PROVIDER, () => new GuidRunIdProvider(), "singleton");
container2.register(TEST_RESULT_NOTIFIER, () => new InMemoryTestResultNotifier(), "singleton"); // <- only line that changed
container2.register(
  TEST_RUN_REPORTER,
  (c) => new TestRunReporter(c.resolve(RUN_ID_PROVIDER), c.resolve(TEST_RESULT_NOTIFIER)),
  "transient"
);

const reporter2 = container2.resolve(TEST_RUN_REPORTER);
reporter2.report("Search_ShouldReturnResults_ForKnownQuery", true);
reporter2.report("Search_ShouldReturnEmpty_ForUnknownQuery", true);

const inMemoryNotifier = container2.resolve(TEST_RESULT_NOTIFIER) as InMemoryTestResultNotifier;
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
