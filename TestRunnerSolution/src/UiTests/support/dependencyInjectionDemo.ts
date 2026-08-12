// Proves that DI works across solution boundaries: everything imported below
// comes from the separate `DependencyInjectionDemo` solution
// (../../../DependencyInjectionDemo), pulled in as an ordinary local npm
// dependency (see package.json -> dependencies -> "dependency-injection-demo").
// UiTests never sees GuidRunIdProvider, ConsoleTestResultNotifier or
// InMemoryTestResultNotifier's source — only the compiled package's public
// API from dist/src/lib.d.ts — yet it can still wire its own container and
// inject its own choice of implementation into TestRunReporter.
//
// Run with: npm run verify-di
import {
  Container,
  RUN_ID_PROVIDER,
  TEST_RESULT_NOTIFIER,
  TEST_RUN_REPORTER,
  GuidRunIdProvider,
  InMemoryTestResultNotifier,
  TestRunReporter,
} from 'dependency-injection-demo';

console.log('=== UiTests consuming the separate DependencyInjectionDemo solution ===\n');

const container = new Container();
container.register(RUN_ID_PROVIDER, () => new GuidRunIdProvider(), 'singleton');
// UiTests picks InMemoryTestResultNotifier -> a choice the DI demo solution
// never makes for us; we're injecting our own implementation into a
// TestRunReporter class that was built and compiled somewhere else.
container.register(TEST_RESULT_NOTIFIER, () => new InMemoryTestResultNotifier(), 'singleton');
container.register(
  TEST_RUN_REPORTER,
  (c) => new TestRunReporter(c.resolve(RUN_ID_PROVIDER), c.resolve(TEST_RESULT_NOTIFIER)),
  'transient'
);

const reporter = container.resolve(TEST_RUN_REPORTER);
reporter.report('UiTests_ImportsExternalPackage', true);
reporter.report('UiTests_InjectsOwnNotifierChoice', true);

const notifier = container.resolve(TEST_RESULT_NOTIFIER) as InMemoryTestResultNotifier;

console.log(`TestRunReporter (built entirely inside DependencyInjectionDemo) recorded ${notifier.received.length} result(s) via UiTests' own injected notifier:`);
for (const { runId, testName, passed } of notifier.received) {
  console.log(`  run=${runId} passed=${passed} test=${testName}`);
}

if (notifier.received.length !== 2) {
  console.error('\nFAILED: expected 2 recorded results.');
  process.exit(1);
}

console.log('\n=== Proven: TestRunnerSolution (UiTests) successfully called into the');
console.log('    separate DependencyInjectionDemo solution and injected its own');
console.log('    dependency into it. ===');
