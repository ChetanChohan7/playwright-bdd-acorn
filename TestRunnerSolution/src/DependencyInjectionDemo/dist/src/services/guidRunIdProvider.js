"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.GuidRunIdProvider = void 0;
const node_crypto_1 = require("node:crypto");
/**
 * Generates one id the moment it's constructed. Because it's registered as a
 * singleton in index.ts, the container only ever constructs one of these per
 * Container instance — proving the same runId is reused everywhere.
 */
class GuidRunIdProvider {
    constructor() {
        this.runId = (0, node_crypto_1.randomUUID)();
    }
}
exports.GuidRunIdProvider = GuidRunIdProvider;
