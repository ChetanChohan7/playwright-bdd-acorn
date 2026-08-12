import { randomUUID } from "node:crypto";
import type { RunIdProvider } from "./types";

/**
 * Generates one id the moment it's constructed. Because it's registered as a
 * singleton in index.ts, the container only ever constructs one of these per
 * Container instance — proving the same runId is reused everywhere.
 */
export class GuidRunIdProvider implements RunIdProvider {
  readonly runId = randomUUID();
}
