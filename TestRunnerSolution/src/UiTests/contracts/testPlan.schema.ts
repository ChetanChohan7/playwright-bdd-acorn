import { z } from "zod";

export interface TestPlanRow {
  id: number;
  name: string;
  status_id: number;
  environment_id: number;
  created_date_time: string;
  assigned_to: string;
  reason: string;
  testing_date_time: string;
  last_modified_date_time: string;
  lock: string;
  locked_by: string;
  locked_at_date_time: string;
  automation_pack_id: number;
}

export const TestPlanSchema = z.object({
  id: z.number(),
  name: z.string(),
  status_id: z.number(),
  environment_id: z.number(),
  created_date_time: z.string(),
  assigned_to: z.string(),
  reason: z.string(),
  testing_date_time: z.string(),
  last_modified_date_time: z.string(),
  lock: z.string(),
  locked_by: z.string(),
  locked_at_date_time: z.string(),
  automation_pack_id: z.number(),
}) satisfies z.ZodType<TestPlanRow>;

export const TestPlanArraySchema = z.array(TestPlanSchema);

/**
 * Deliberately incompatible schema for negative-path validation.
 * This is intentionally wrong for the real test_plans rows.
 */
export const IncompatibleTestPlanSchema = z.object({
  id: z.number(),
  name: z.string(),
  status_id: z.string(), // intentionally wrong
  environment_id: z.number(),
  created_date_time: z.string(),
  assigned_to: z.string(),
  reason: z.string(),
  testing_date_time: z.string(),
  last_modified_date_time: z.string(),
  lock: z.string(),
  locked_by: z.string(),
  locked_at_date_time: z.string(),
  automation_pack_id: z.string(), // intentionally wrong
});

export const IncompatibleTestPlanArraySchema = z.array(IncompatibleTestPlanSchema);

