# DependencyInjectionDemo

A small, self-contained TypeScript project that proves dependency injection
(DI) works. TypeScript has no runtime reflection on interfaces (unlike C#'s
`Microsoft.Extensions.DependencyInjection`), so this project includes a tiny
hand-rolled container ([`src/container.ts`](src/container.ts)) that keys
registrations on typed `Token<T>`s instead of interface types — the same
"register a factory, resolve it later, honour lifetimes" mechanics, just
without a runtime type system to lean on.

## The shape of the example

- **[`src/services/types.ts`](src/services/types.ts)** — the abstractions:
  `RunIdProvider`, `TestResultNotifier`.
- **[`src/services/consoleTestResultNotifier.ts`](src/services/consoleTestResultNotifier.ts)**,
  **[`src/services/inMemoryTestResultNotifier.ts`](src/services/inMemoryTestResultNotifier.ts)**,
  **[`src/services/guidRunIdProvider.ts`](src/services/guidRunIdProvider.ts)** —
  concrete implementations.
- **[`src/testRunReporter.ts`](src/testRunReporter.ts)** — the *consumer*. Its
  constructor takes a `RunIdProvider` and a `TestResultNotifier`. It never
  instantiates a concrete class itself, and never says `new` on either
  dependency.
- **[`src/index.ts`](src/index.ts)** — wires everything up through
  `Container` and prints two proofs.

## Run it

```bash
cd src/DependencyInjectionDemo
npm install
npm start
```

### Proof 1 — the container builds `TestRunReporter`'s dependencies for it

`TestRunReporter` is registered `"transient"` (new instance every resolve)
while `RunIdProvider` is `"singleton"` (one instance for the whole
`Container`). The output shows this lifetime behaviour is actually honoured:

```
TestRunReporter is transient -> two resolves give different instances: true
RunIdProvider is singleton  -> two resolves give the same instance: true
```

### Proof 2 — swap the injected implementation, zero changes to the consumer

The second half of `index.ts` builds a *second* container where the only
difference is one registration line:

```ts
container2.register(TEST_RESULT_NOTIFIER, () => new InMemoryTestResultNotifier(), "singleton");
// instead of ConsoleTestResultNotifier
```

`TestRunReporter`'s source is not touched at all, yet its behaviour changes —
the results it reports now land in an in-memory array instead of the
console, because the container is what decided which concrete class
`TestRunReporter` received.

## Run the tests (DI without a container at all)

```bash
npm test
```

`tests/testRunReporter.test.ts` constructs `TestRunReporter` directly
(`new TestRunReporter(fakeRunIdProvider, fakeNotifier)`) — no `Container` in
sight. This is only possible because `TestRunReporter` depends on
interfaces, not concrete classes: the tests hand it disposable fakes
(`FixedRunIdProvider`, `InMemoryTestResultNotifier`) instead of the real
console-writing implementation, proving the same decoupling that makes
Proof 2 above possible also makes the class trivially unit-testable.
