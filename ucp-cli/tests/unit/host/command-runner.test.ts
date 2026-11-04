import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { CommandRunner, type Dispatch } from "#host/commands/command-runner.js";
import { Logger } from "#host/logger/logger.js";
import { SessionDirectory } from "#host/sessions/session-directory.js";
import type { QueuedCommand, UcpSession } from "#shared/types.js";

const logger = new Logger({ level: "error" });

function session(overrides: Partial<UcpSession> = {}): UcpSession {
  return {
    id: "editor-1",
    name: "demo",
    path: "C:/Project/Demo",
    type: "editor",
    status: "ready",
    capabilities: ["ping", "compile", "echo"],
    updatedAt: new Date().toISOString(),
    ...overrides,
  };
}

function sleep(ms: number) {
  return new Promise((r) => setTimeout(r, ms));
}

const runners: CommandRunner[] = [];

function setup(
  dispatch: Dispatch,
  sessions: UcpSession[] = [session()],
  detachedCancelMs?: number,
) {
  const directory = new SessionDirectory(
    logger,
    fs.mkdtempSync(path.join(os.tmpdir(), "ucp-run-")),
  );
  directory.set(sessions);
  const runner = new CommandRunner(directory, dispatch, logger, detachedCancelMs);
  runner.start();
  runners.push(runner);
  return { directory, runner };
}

async function submit(runner: CommandRunner, body: Parameters<CommandRunner["submit"]>[0]) {
  const outcome = await runner.submit(body);
  if (!outcome.ok) throw new Error(`rejected: ${outcome.error}`);
  return outcome.entry;
}

async function finished(runner: CommandRunner, entry: QueuedCommand): Promise<QueuedCommand> {
  for (;;) {
    const current = await runner.wait(entry.command.id, 5_000);
    if (current && current.completedAt !== undefined) return current;
  }
}

const pong: Dispatch = async (_, command) => ({ id: command.id, success: true, duration: 1 });

afterEach(() => {
  for (const runner of runners.splice(0)) runner.stop();
});

describe("CommandRunner", () => {
  it("runs one command at a time per session and sessions side by side", async () => {
    const log: string[] = [];
    const dispatch: Dispatch = async (s, command) => {
      log.push(`start ${s.id} ${command.args?.n}`);
      await sleep(100);
      log.push(`end ${s.id} ${command.args?.n}`);
      return { id: command.id, success: true, duration: 100 };
    };
    const { runner } = setup(dispatch, [
      session(),
      session({ id: "runtime-1", type: "runtime", runtimePort: 6620 }),
    ]);

    const a = await submit(runner, { type: "ping", session: "editor-1", args: { n: 1 } });
    const b = await submit(runner, { type: "ping", session: "editor-1", args: { n: 2 } });
    const c = await submit(runner, { type: "ping", session: "runtime-1", args: { n: 3 } });
    await Promise.all([a, b, c].map((e) => finished(runner, e)));

    // Same session: 2 starts only after 1 ended. Other session: 3 started before 1 ended.
    expect(log.indexOf("start editor-1 2")).toBeGreaterThan(log.indexOf("end editor-1 1"));
    expect(log.indexOf("start runtime-1 3")).toBeLessThan(log.indexOf("end editor-1 1"));
  });

  it("wait returns as soon as the result is in, not when the wait runs out", async () => {
    const { runner } = setup(async (_, command) => {
      await sleep(150);
      return { id: command.id, success: true, duration: 150, message: "pong" };
    });
    const entry = await submit(runner, { type: "ping" });

    const started = Date.now();
    const done = await runner.wait(entry.command.id, 20_000);
    expect(Date.now() - started).toBeLessThan(1_000);
    expect(done?.status).toBe("completed");
    expect(done?.result?.message).toBe("pong");
    expect(done?.timing?.run_ms).toBe(150);
  });

  it("waits for a busy editor whose process is alive", async () => {
    const { runner, directory } = setup(pong, [session({ status: "busy", pid: process.pid })]);
    const entry = await submit(runner, { type: "ping", timeout: 5_000 });

    await sleep(300);
    expect(runner.get(entry.command.id)?.status).toBe("queued");
    directory.set([session({ pid: process.pid })]);

    const done = await finished(runner, entry);
    expect(done.status).toBe("completed");
    expect(done.timing?.waited_for_session).toBe(true);
    expect(done.timing?.queued_ms).toBeGreaterThanOrEqual(250);
  });

  it("fails at once when the editor's process has exited", async () => {
    const deadPid = spawnSync(process.execPath, ["-e", ""]).pid;
    const { runner } = setup(pong, [session({ status: "offline", pid: deadPid })]);

    const outcome = await runner.submit({ type: "ping", project: "C:/Project/Demo" });
    expect(outcome).toMatchObject({ ok: false, error: "editor_not_running" });
  });

  it("does not wait for a runtime: no ready one means no_ready_session", async () => {
    const { runner } = setup(pong, [
      session({ pid: process.pid }),
      session({ id: "runtime-1", type: "runtime", status: "offline" }),
    ]);

    const outcome = await runner.submit({ type: "echo", sessionType: "runtime" });
    expect(outcome).toMatchObject({ ok: false, error: "no_ready_session" });
  });

  it("counts the deadline from submission, so a command expires while queued", async () => {
    let dispatches = 0;
    const { runner } = setup(async (_, command) => {
      dispatches += 1;
      await sleep(400);
      return { id: command.id, success: true, duration: 400 };
    });
    const first = await submit(runner, { type: "ping", timeout: 5_000 });
    const second = await submit(runner, { type: "ping", timeout: 200 });

    expect((await finished(runner, second)).status).toBe("timeout");
    expect((await finished(runner, first)).status).toBe("completed");
    expect(dispatches).toBe(1);
  });

  it("marks a timed-out command and never dispatches it again", async () => {
    let dispatches = 0;
    const { runner } = setup(() => {
      dispatches += 1;
      return new Promise<never>(() => {});
    });
    const entry = await submit(runner, { type: "ping", timeout: 100 });

    const done = await finished(runner, entry);
    await sleep(500);
    expect(done.status).toBe("timeout");
    expect(dispatches).toBe(1);
  });

  it("says so when a timeout happened with Unity in the background", async () => {
    const { runner } = setup(() => new Promise<never>(() => {}), [session({ focused: false })]);
    const done = await finished(runner, await submit(runner, { type: "ping", timeout: 100 }));
    expect(done.hint).toContain("background");
  });

  it("hands Unity what is left of the timeout, and applies the command's own default", async () => {
    const seen: number[] = [];
    const { runner } = setup(
      async (_, command) => {
        seen.push(command.timeout);
        return { id: command.id, success: true, duration: 1 };
      },
      [session({ defaultTimeouts: { compile: 180_000 } })],
    );

    const compile = await submit(runner, { type: "compile" });
    const ping = await submit(runner, { type: "ping" });
    await Promise.all([finished(runner, compile), finished(runner, ping)]);

    expect(compile.command.timeout).toBe(180_000);
    expect(ping.command.timeout).toBe(30_000);
    expect(seen[0]).toBeLessThanOrEqual(180_000);
    expect(seen[0]).toBeGreaterThan(179_000);
  });

  it("drops a queued command nobody is waiting for any more", async () => {
    let dispatches = 0;
    const { runner } = setup(
      async (_, command) => {
        dispatches += 1;
        return { id: command.id, success: true, duration: 1 };
      },
      [session({ status: "busy", pid: process.pid })],
      200,
    );
    const entry = await submit(runner, { type: "ping", timeout: 10_000 });

    await sleep(1_500);
    const done = runner.get(entry.command.id);
    expect(done?.status).toBe("cancelled");
    expect(done?.error).toBe("caller_disconnected");
    expect(dispatches).toBe(0);
  });

  it("rejects a command type the session does not advertise", async () => {
    const { runner } = setup(pong);
    const done = await finished(runner, await submit(runner, { type: "build" }));
    expect(done.status).toBe("failed");
    expect(done.hint).toMatch(/does not support/);
  });
});
