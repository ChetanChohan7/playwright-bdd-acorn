/**
 * A tiny, dependency-free DI container. TypeScript interfaces don't exist at
 * runtime (they're erased at compile time), so — unlike C#'s
 * `Microsoft.Extensions.DependencyInjection`, which can key a registration on
 * an interface type — this container keys registrations on a `Token<T>`
 * instead. Everything else (register a factory, resolve it later, honour
 * singleton vs. transient lifetimes) works the same way.
 */

export type Lifetime = "singleton" | "transient";

/** A typed key. The `__type` field never actually holds a value — it exists
 *  purely so TypeScript can infer what `container.resolve(token)` returns. */
export type Token<T> = symbol & { readonly __type?: T };

export function createToken<T>(description: string): Token<T> {
  return Symbol(description) as Token<T>;
}

type Factory<T> = (container: Container) => T;

interface Registration<T> {
  factory: Factory<T>;
  lifetime: Lifetime;
  instance?: T;
}

export class Container {
  private readonly registrations = new Map<Token<unknown>, Registration<unknown>>();

  register<T>(token: Token<T>, factory: Factory<T>, lifetime: Lifetime): void {
    this.registrations.set(token, { factory, lifetime });
  }

  resolve<T>(token: Token<T>): T {
    const registration = this.registrations.get(token) as Registration<T> | undefined;
    if (!registration) {
      throw new Error(`No registration found for token: ${token.toString()}`);
    }

    if (registration.lifetime === "transient") {
      return registration.factory(this);
    }

    // singleton: build it once, cache it, reuse it forever after.
    if (registration.instance === undefined) {
      registration.instance = registration.factory(this);
    }
    return registration.instance;
  }
}
