import { test } from "node:test";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { TestRunReporter } from "../src/testRunReporter";
import { InMemoryTestResultNotifier } from "../src/services/inMemoryTestResultNotifier";
import type { RunIdProvider } from "../src/services/types";

/**
 * A third implementation of RunIdProvider, written only for tests, that
 * GuidRunIdProvider and TestRunReporter's own code know nothing about.
 * Being able to write and inject this — without touching any production
 * class — is the payoff of coding against the RunIdProvider abstraction.
 */
class FixedRunIdProvider implements RunIdProvider {
  constructor(readonly runId: string) {}
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

test("report() passes the runId from the injected provider to the injected notifier", () => {
  const runId = "11111111-1111-1111-1111-111111111111";
  const runIdProvider = new FixedRunIdProvider(runId);
  const notifier = new InMemoryTestResultNotifier();
  const reporter = new TestRunReporter(runIdProvider, notifier);

  reporter.report("Sample_Test", true);

  assert.equal(notifier.received.length, 1);
  assert.deepEqual(notifier.received[0], { runId, testName: "Sample_Test", passed: true });
});

test("report() uses whichever notifier was injected, not a hardcoded one", () => {
  const runIdProvider = new FixedRunIdProvider(randomUUID());
  const notifierA = new InMemoryTestResultNotifier();
  const notifierB = new InMemoryTestResultNotifier();

  // Same TestRunReporter class, two different injected notifiers.
  const reporterWithA = new TestRunReporter(runIdProvider, notifierA);
  const reporterWithB = new TestRunReporter(runIdProvider, notifierB);

  reporterWithA.report("Only_For_A", true);
  reporterWithB.report("Only_For_B", false);

  assert.equal(notifierA.received.length, 1);
  assert.equal(notifierA.received[0].testName, "Only_For_A");

  assert.equal(notifierB.received.length, 1);
  assert.equal(notifierB.received[0].testName, "Only_For_B");
});

test("report() called multiple times forwards each result in order", () => {
  const runIdProvider = new FixedRunIdProvider(randomUUID());
  const notifier = new InMemoryTestResultNotifier();
  const reporter = new TestRunReporter(runIdProvider, notifier);

  reporter.report("First", true);
  reporter.report("Second", false);
  reporter.report("Third", true);

  assert.deepEqual(
    notifier.received.map((r) => r.testName),
    ["First", "Second", "Third"]
  );
  assert.deepEqual(
    notifier.received.map((r) => r.passed),
    [true, false, true]
  );
});
