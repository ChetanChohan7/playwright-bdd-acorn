#!/bin/zsh
set -euo pipefail

ROOT="${1:-/Users/chetanchohan/Projects/inspired-poc/TestRunnerSolution/src/TestDataService}"
MODE="${2:-all}"

# Allow calling as:
# ./run-testdata-tests.sh api
# ./run-testdata-tests.sh ui
# ./run-testdata-tests.sh all
# or
# ./run-testdata-tests.sh /full/path/to/TestDataService api
if [[ "$ROOT" == "api" || "$ROOT" == "ui" || "$ROOT" == "all" ]]; then
  MODE="$ROOT"
  ROOT="/Users/chetanchohan/Projects/inspired-poc/TestRunnerSolution/src/TestDataService"
fi

TESTS_DIR="$ROOT/Tests"
API_URL="http://localhost:5111"
PID_FILE="$TESTS_DIR/.testdataservice.pid"

require_dir() {
  local dir="$1"
  if [[ ! -d "$dir" ]]; then
    echo "Required directory not found: $dir" >&2
    exit 1
  fi
}

require_file() {
  local file="$1"
  if [[ ! -f "$file" ]]; then
    echo "Required file not found: $file" >&2
    exit 1
  fi
}

wait_for_api() {
  local attempts=30
  local count=1
  while [[ $count -le $attempts ]]; do
    if curl -fsS "$API_URL/health" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
    count=$((count + 1))
  done
  return 1
}

cleanup() {
  if [[ -f "$PID_FILE" ]]; then
    local pid
    pid="$(cat "$PID_FILE")"
    if ps -p "$pid" >/dev/null 2>&1; then
      kill "$pid" >/dev/null 2>&1 || true
    fi
    rm -f "$PID_FILE"
  fi
}

trap cleanup EXIT

require_dir "$ROOT"
require_dir "$TESTS_DIR"
require_file "$ROOT/TestDataService.csproj"
require_file "$TESTS_DIR/package.json"
require_file "$TESTS_DIR/playwright.config.ts"

echo ""
echo "==> Building TestDataService"
cd "$ROOT"
dotnet build

echo ""
echo "==> Installing Node dependencies"
cd "$TESTS_DIR"
npm install

echo ""
echo "==> Starting TestDataService"
cd "$ROOT"
dotnet run > "$TESTS_DIR/testdataservice.log" 2>&1 &
echo $! > "$PID_FILE"

echo ""
echo "==> Waiting for API at $API_URL"
if ! wait_for_api; then
  echo "TestDataService did not become ready in time." >&2
  echo "Check log: $TESTS_DIR/testdataservice.log" >&2
  exit 1
fi

echo ""
echo "==> Running tests: $MODE"
cd "$TESTS_DIR"

case "$MODE" in
  api)
    npm run test:api
    ;;
  ui)
    npm run test:ui
    ;;
  all)
    npm run test
    ;;
  *)
    echo "Invalid mode: $MODE" >&2
    echo "Use one of: api, ui, all" >&2
    exit 1
    ;;
esac

echo ""
echo "==> Completed successfully"
