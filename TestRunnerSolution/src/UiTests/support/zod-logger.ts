import { ZodSchema } from 'zod';

type IssueSummary = {
  key: string;
  path: string;
  message: string;
  count: number;
};

function formatPath(path: PropertyKey[]) {
  if (!path.length) return '(root)';
  return path.map(part => typeof part === 'symbol' ? part.toString() : String(part)).join('.');
}

export function validateWithConciseLogging<T>(
  schema: ZodSchema<T>,
  payload: unknown,
  options?: {
    label?: string;
    sampleLimit?: number;
  }
): T {
  const result = schema.safeParse(payload);

  if (result.success) {
    return result.data;
  }

  const label = options?.label ?? 'Schema';
  const sampleLimit = options?.sampleLimit ?? 6;

  const summaries = new Map<string, IssueSummary>();

  for (const issue of result.error.issues) {
    const path = formatPath(issue.path);
    const key = `${path}__${issue.message}`;

    if (!summaries.has(key)) {
      summaries.set(key, {
        key,
        path,
        message: issue.message,
        count: 1,
      });
    } else {
      summaries.get(key)!.count += 1;
    }
  }

  const summaryList = [...summaries.values()];

  console.error(`Schema validation failed for ${label}`);

  if (Array.isArray(payload)) {
    console.error(`Rows checked: ${payload.length}`);
  }

  console.error('Unique mismatches:');
  for (const item of summaryList) {
    console.error(`- ${item.path}: ${item.message} (${item.count} occurrence${item.count === 1 ? '' : 's'})`);
  }

  console.error('Example mismatches:');
  for (const issue of result.error.issues.slice(0, sampleLimit)) {
    console.error(`- ${formatPath(issue.path)} -> ${issue.message}`);
  }

  throw new Error(
    `${label} validation failed with ${result.error.issues.length} issue${result.error.issues.length === 1 ? '' : 's'}`
  );
}
