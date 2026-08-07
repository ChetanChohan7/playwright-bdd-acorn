// src/UiTests/support/normalisers/testData.normaliser.ts
export function normaliseRows(input: unknown): Record<string, unknown>[] {
  const rows = Array.isArray(input) ? input : input ? [input] : [];

  return rows
    .map((row: any) => ({
      id: row.id ?? null,
      name: row.name ?? null,
      status: row.status ?? null,
    }))
    .sort((a, b) => String(a.id).localeCompare(String(b.id)));
}
