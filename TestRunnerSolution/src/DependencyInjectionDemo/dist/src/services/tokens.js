"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.TEST_RESULT_NOTIFIER = exports.RUN_ID_PROVIDER = void 0;
const container_1 = require("../container");
exports.RUN_ID_PROVIDER = (0, container_1.createToken)("RunIdProvider");
exports.TEST_RESULT_NOTIFIER = (0, container_1.createToken)("TestResultNotifier");
