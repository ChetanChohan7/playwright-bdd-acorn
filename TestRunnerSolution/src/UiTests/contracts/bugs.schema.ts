import { z } from "zod";

export interface BugRow {
  id: number;
  summary: string;
  description: string | null;
  status_id: number;
  root_cause: string | null;
  raised_by: string | null;
  created_date_time: string;
  last_modified_date_time: string;
  closed_date_time: string;
  external_id: string | null;
  external_url: string | null;
}

export const BugSchema = z.object({
  id: z.number(),
  summary: z.string(),
  description: z.string().nullable(),
  status_id: z.number(),
  root_cause: z.string().nullable(),
  raised_by: z.string().nullable(),
  created_date_time: z.string(),
  last_modified_date_time: z.string(),
  closed_date_time: z.string(),
  external_id: z.string().nullable(),
  external_url: z.string().nullable(),
}) satisfies z.ZodType<BugRow>;

export const BugArraySchema = z.array(BugSchema);

/**
 * Deliberately incompatible schema for negative-path validation.
 * This is intentionally wrong for the real bugs rows.
 */
export const IncompatibleBugSchema = z.object({
  id: z.number(),
  summary: z.string(),
  description: z.string().nullable(),
  status_id: z.string(), // intentionally wrong
  root_cause: z.string().nullable(),
  raised_by: z.string().nullable(),
  created_date_time: z.string(),
  last_modified_date_time: z.string(),
  closed_date_time: z.string(),
  external_id: z.number().nullable(), // intentionally wrong
  external_url: z.string().nullable(),
});

export const IncompatibleBugArraySchema = z.array(IncompatibleBugSchema);
