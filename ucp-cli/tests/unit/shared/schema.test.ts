import { describe, expect, it } from "vitest";
import { submitCommandSchema } from "#shared/schema.js";

describe("submitCommandSchema", () => {
  it("accepts valid command body", () => {
    const result = submitCommandSchema.safeParse({
      type: "ping",
      timeout: 5000,
      project: "C:/Game",
    });
    expect(result.success).toBe(true);
  });

  it("rejects empty type", () => {
    const result = submitCommandSchema.safeParse({ type: "" });
    expect(result.success).toBe(false);
  });
});
