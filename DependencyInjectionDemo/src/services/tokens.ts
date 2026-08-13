import { createToken } from "../container";
import type { RunIdProvider, TestResultNotifier } from "./types";

export const RUN_ID_PROVIDER = createToken<RunIdProvider>("RunIdProvider");
export const TEST_RESULT_NOTIFIER = createToken<TestResultNotifier>("TestResultNotifier");
