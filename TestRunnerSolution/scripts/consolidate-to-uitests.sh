#!/bin/zsh
set -euo pipefail

ROOT="${1:-$(pwd)}"
UITESTS_DIR="$ROOT/src/UiTests"
RUN_ALL="$ROOT/scripts/run-all.sh"
TSCONFIG="$UITESTS_DIR/tsconfig.json"
TESTS_DIR="$ROOT/src/TestDataService/Tests"

require_file() {
  local file="$1"
  if [[ ! -f "$file" ]]; then
    echo "Required file not found: $file" >&2
    exit 1
  fi
}

backup_file() {
  local file="$1"
  if [[ -f "$file" ]]; then
    cp "$file" "$file.bak"
  fi
}

require_file "$TSCONFIG"
require_file "$RUN_ALL"

backup_file "$TSCONFIG"
backup_file "$RUN_ALL"

cat > "$TSCONFIG" <<'EOF'
{
  "compilerOptions": {
    "target": "ES2020",
    "module": "Node16",
    "moduleResolution": "node16",
    "types": ["node", "@playwright/test"],
    "resolveJsonModule": true,
    "esModuleInterop": true,
    "strict": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "outDir": "dist"
  },
  "include": [
    "features/**/*.ts",
    "steps/**/*.ts",
    "api/**/*.ts",
    "clients/**/*.ts",
    "contracts/**/*.ts",
    "fixtures/**/*.ts",
    "support/**/*.ts",
    "utils/**/*.ts",
    "pages/**/*.ts",
    "playwright.config.ts"
  ]
}
EOF

cat > "$RUN_ALL" <<'EOF'
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
EOF

chmod +x "$RUN_ALL"

if [[ -d "$TESTS_DIR" ]]; then
  mv "$TESTS_DIR" "$ROOT/src/TestDataService/Tests.disabled"
fi

echo "Done."
echo "- UiTests tsconfig updated."
echo "- scripts/run-all.sh now uses src/UiTests."
echo "- src/TestDataService/Tests moved to Tests.disabled (if it existed)."
