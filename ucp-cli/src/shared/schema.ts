import { z } from "zod";

export const editorTargetSchema = z.object({
  id: z.string().min(1),
  type: z.literal("editor"),
  name: z.string().optional(),
  projectPath: z.string().min(1),
  queueId: z.string().optional(),
});

export const runtimeTargetSchema = z.object({
  id: z.string().min(1),
  type: z.literal("runtime"),
  name: z.string().optional(),
  host: z.string().min(1),
  port: z.number().int().positive(),
  projectPath: z.string().optional(),
});

export const ucpTargetSchema = z.discriminatedUnion("type", [
  editorTargetSchema,
  runtimeTargetSchema,
]);

export const targetsFileSchema = z.object({
  targets: z.array(ucpTargetSchema),
});

export const submitCommandSchema = z.object({
  type: z.string().min(1),
  session: z.string().optional(),
  target: z.string().optional(),
  project: z.string().optional(),
  sessionType: z.enum(["editor", "runtime"]).optional(),
  timeout: z.number().int().positive().optional(),
  args: z.record(z.unknown()).optional(),
});

export type SubmitCommandInput = z.infer<typeof submitCommandSchema>;

export const ucpSessionSchema = z.object({
  id: z.string(),
  name: z.string(),
  path: z.string(),
  type: z.enum(["editor", "runtime"]),
  status: z.enum(["offline", "ready", "busy"]),
  capabilities: z.array(z.string()),
  updatedAt: z.string(),
  host: z.string().optional(),
  runtimePort: z.number().optional(),
  queueId: z.string().optional(),
});

export const ucpResultSchema = z.object({
  id: z.string(),
  success: z.boolean(),
  duration: z.number(),
  message: z.string().optional(),
  data: z.record(z.unknown()).optional(),
  error: z.string().optional(),
});
