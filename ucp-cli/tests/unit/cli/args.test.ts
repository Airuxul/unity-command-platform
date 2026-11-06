import { afterEach, describe, expect, it } from "vitest";
import { run } from "#cli/main.js";

describe("ucp-cli argument checks", () => {
  const original = console.log;

  afterEach(() => {
    console.log = original;
    process.exitCode = undefined;
  });

  async function capture(argv: string[]): Promise<Record<string, unknown>> {
    const logs: string[] = [];
    console.log = (line?: unknown) => {
      logs.push(String(line));
    };
    process.exitCode = undefined;
    await run(argv);
    return JSON.parse(logs[0] ?? "{}") as Record<string, unknown>;
  }

  it("rejects an unknown --session-type before talking to the host", async () => {
    const body = await capture(["--session-type", "device", "ping"]);
    expect(process.exitCode).toBe(1);
    expect(body).toMatchObject({
      ok: false,
      error_code: "invalid_argument",
    });
    expect(String(body.message)).toContain("device");
  });

  it("rejects run --args that is not a JSON object", async () => {
    const body = await capture(["run", "ping", "--args", "not-json"]);
    expect(process.exitCode).toBe(1);
    expect(body.error_code).toBe("invalid_argument");
  });
});
