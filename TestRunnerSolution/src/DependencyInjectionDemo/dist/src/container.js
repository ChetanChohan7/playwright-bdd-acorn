"use strict";
/**
 * A tiny, dependency-free DI container. TypeScript interfaces don't exist at
 * runtime (they're erased at compile time), so — unlike C#'s
 * `Microsoft.Extensions.DependencyInjection`, which can key a registration on
 * an interface type — this container keys registrations on a `Token<T>`
 * instead. Everything else (register a factory, resolve it later, honour
 * singleton vs. transient lifetimes) works the same way.
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.Container = void 0;
exports.createToken = createToken;
function createToken(description) {
    return Symbol(description);
}
class Container {
    constructor() {
        this.registrations = new Map();
    }
    register(token, factory, lifetime) {
        this.registrations.set(token, { factory, lifetime });
    }
    resolve(token) {
        const registration = this.registrations.get(token);
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
exports.Container = Container;
