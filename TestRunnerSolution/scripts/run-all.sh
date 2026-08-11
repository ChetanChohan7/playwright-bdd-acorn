#!/bin/zsh
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
API_URL="http://localhost:5111"
UITESTS_DIR="$ROOT_DIR/src/UiTests"

(cd "$ROOT_DIR/src/TestDataService" && dotnet run) &
API_PID=$!

cleanup() {
  kill "$API_PID" 2>/dev/null || true
}
trap cleanup EXIT

printf 'Waiting for TestDataService to start'
for _ in {1..30}; do
  if curl -sf "$API_URL/health" >/dev/null 2>&1; then
    break
  fi
  printf '.'
  sleep 1
done
printf '\n'

cd "$UITESTS_DIR"
cp -n .env.example .env || true
npm install
npm test
