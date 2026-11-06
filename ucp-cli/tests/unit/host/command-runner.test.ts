import { spawnSync } from "node:child_process";
import fs from "node:fs";
import http from "node:http";
import type { AddressInfo } from "node:net";
import os from "node:os";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { CommandRunner, type Dispatch } from "#host/commands/command-runner.js";
import { HostDispatchError, HostError } from "#host/errors.js";
import { Logger } from "#host/logger/logger.js";
import { SessionDirectory } from "#host/sessions/session-directory.js";
import { writeJsonAtomic } from "#shared/index.js";
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
  it("records a dispatch that never reached Unity as failed", async () => {
    const { runner } = setup(async () => {
      throw new HostDispatchError(
        HostError.unknownSessionType,
        "No transport for session type 'device'.",
      );
    });
    const entry = await submit(runner, { type: "ping" });
    const done = await finished(runner, entry);
    expect(done.status).toBe("failed");
    expect(done.error).toBe(HostError.unknownSessionType);
    expect(done.result).toBeUndefined();
  });

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

  it("probes a runtime again before turning a command for it away", async () => {
    // Play Mode has just started: the runtime answers, but the last probe still says offline.
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-runtime-"));
    const runtime = http.createServer((_, res) => {
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: true, capabilities: ["echo"] }));
    });
    await new Promise<void>((r) => runtime.listen(0, "127.0.0.1", r));
    const port = (runtime.address() as AddressInfo).port;
    writeJsonAtomic(path.join(root, "targets.json"), {
      targets: [
        { id: "game", type: "runtime", host: "127.0.0.1", port, projectPath: "C:/Project/Demo" },
      ],
    });
    const directory = new SessionDirectory(logger, root);
    directory.set([session({ id: "game", type: "runtime", status: "offline", runtimePort: port })]);
    const runner = new CommandRunner(directory, pong, logger);
    runners.push(runner);
    runner.start();
    try {
      for (const body of [
        { type: "echo", target: "game" },
        { type: "echo", sessionType: "runtime" as const, project: "C:/Project/Demo" },
      ]) {
        const outcome = await runner.submit(body);
        expect(outcome.ok, JSON.stringify(outcome)).toBe(true);
        if (outcome.ok) expect((await finished(runner, outcome.entry)).status).toBe("completed");
      }
    } finally {
      runtime.close();
    }
  });

  it("turns a runtime command away within the probe timeout when the device does not answer", async () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "ucp-deadrt-"));
    const device = http.createServer(() => {});
    await new Promise<void>((r) => device.listen(0, "127.0.0.1", r));
    const port = (device.address() as AddressInfo).port;
    writeJsonAtomic(path.join(root, "targets.json"), {
      targets: [{ id: "game", type: "runtime", host: "127.0.0.1", port }],
    });
    const directory = new SessionDirectory(logger, root);
    directory.set([session({ id: "game", type: "runtime", status: "offline", runtimePort: port })]);
    const runner = new CommandRunner(directory, pong, logger);
    runners.push(runner);
    try {
      const started = Date.now();
      const outcome = await runner.submit({ type: "echo", sessionType: "runtime" });
      const waited = Date.now() - started;
      expect(outcome).toMatchObject({ ok: false, error: "no_ready_session" });
      // The re-probe is what costs: PROBE_TIMEOUT_MS (3 s), never the caller's whole timeout.
      expect(waited).toBeGreaterThanOrEqual(2_500);
      expect(waited).toBeLessThan(4_500);
    } finally {
      device.closeAllConnections();
      device.close();
    }
  }, 15_000);

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

  it("fails a command type the session does not advertise as unknown_command", async () => {
    const { runner } = setup(pong);
    const done = await finished(runner, await submit(runner, { type: "build" }));
    expect(done).toMatchObject({ status: "failed", error: "host_unknown_command" });
    expect(done.message).toContain("build");
    expect(done.hint).toContain("help");
  });

  it("gives every failure the host decides a code and a message", async () => {
    const hang: Dispatch = () => new Promise<never>(() => {});
    const failures: QueuedCommand[] = [];

    const timedOut = setup(hang).runner;
    failures.push(await finished(timedOut, await submit(timedOut, { type: "ping", timeout: 100 })));

    const thrown = setup(async () => {
      throw new Error("connect ECONNREFUSED 127.0.0.1:6620");
    }).runner;
    failures.push(await finished(thrown, await submit(thrown, { type: "ping" })));

    const { runner: gone, directory } = setup(pong, [
      session({ status: "busy", pid: process.pid }),
    ]);
    const waiting = await submit(gone, { type: "ping", timeout: 5_000 });
    directory.set([session({ status: "offline", pid: process.pid })]);
    failures.push(await finished(gone, waiting));

    expect(failures.map((f) => f.error)).toEqual([
      "host_timeout",
      "host_command_failed",
      "editor_not_running",
    ]);
    for (const failure of failures) expect(failure.message, failure.error).toBeTruthy();
    expect(failures[1].message).toContain("ECONNREFUSED");

    const rejected = await setup(pong, []).runner.submit({ type: "ping" });
    expect(rejected).toMatchObject({ ok: false, error: "no_ready_session" });
    expect(!rejected.ok && rejected.message).toBeTruthy();
  });

  it("times out a queued command at its deadline, not when the command ahead of it ends", async () => {
    const { runner } = setup(() => new Promise<never>(() => {}));
    await submit(runner, { type: "ping", timeout: 30_000 });
    const behind = await submit(runner, { type: "ping", timeout: 300 });

    const started = Date.now();
    const done = await finished(runner, behind);
    expect(done).toMatchObject({ status: "timeout", error: "host_timeout" });
    expect(Date.now() - started).toBeLessThan(2_000);
    expect(done.message).toBeTruthy();
  });

  it("lets a read-only command past a command still running on the same editor", async () => {
    let release = () => {};
    const testRun = new Promise<void>((r) => {
      release = r;
    });
    const order: string[] = [];
    const { runner } = setup(
      async (_, command) => {
        if (command.type === "test_run") await testRun;
        order.push(command.type);
        return { id: command.id, success: true, duration: 1 };
      },
      [session({ capabilities: ["test_run", "state", "ping"], runsWhileBusy: ["state"] })],
    );

    const long = await submit(runner, { type: "test_run", timeout: 10_000 });
    const state = await submit(runner, { type: "state", timeout: 1_000 });
    const ping = await submit(runner, { type: "ping", timeout: 10_000 });

    expect((await finished(runner, state)).status).toBe("completed");
    // ping is not read-only: it keeps its place behind test_run.
    expect(runner.get(ping.command.id)?.status).toBe("queued");

    release();
    await Promise.all([finished(runner, long), finished(runner, ping)]);
    expect(order).toEqual(["state", "test_run", "ping"]);
  });

  it("keeps every command in line for an agent that names no read-only commands", async () => {
    const { runner } = setup(
      (_, command) =>
        command.type === "test_run"
          ? new Promise<never>(() => {})
          : Promise.resolve({ id: command.id, success: true, duration: 1 }),
      [session({ capabilities: ["test_run", "state"] })],
    );
    await submit(runner, { type: "test_run", timeout: 10_000 });
    const state = await finished(runner, await submit(runner, { type: "state", timeout: 300 }));
    expect(state.status).toBe("timeout");
  });

  it("hands Unity the deadline as well as what is left of the timeout", async () => {
    const seen: number[] = [];
    const { runner } = setup(async (_, command) => {
      seen.push(command.deadline ?? 0);
      return { id: command.id, success: true, duration: 1 };
    });
    const entry = await submit(runner, { type: "ping", timeout: 5_000 });
    await finished(runner, entry);
    expect(seen).toEqual([entry.deadline]);
  });

  it("runs before and after commands around the one that was submitted", async () => {
    const seen: { type: string; args: unknown; id: string }[] = [];
    const { runner } = setup(
      async (_, command) => {
        seen.push({ type: command.type, args: command.args, id: command.id });
        return { id: command.id, success: true, duration: 1, message: command.type };
      },
      [
        session({
          capabilities: ["play", "compile", "echo"],
          commandChains: { play: { before: ["compile"], after: ["echo"] } },
        }),
      ],
    );
    const entry = await submit(runner, {
      type: "play",
      timeout: 5_000,
      args: { scene: "Assets/Main.unity" },
    });
    const done = await finished(runner, entry);
    expect(seen.map((s) => s.type)).toEqual(["compile", "play", "echo"]);
    expect(seen[0]?.args).toEqual({});
    expect(seen[1]?.args).toEqual({ scene: "Assets/Main.unity" });
    expect(seen[1]?.id).toBe(entry.command.id);
    expect(done.result?.success).toBe(true);
    expect(done.result?.message).toBe("play");
    expect(done.result?.id).toBe(entry.command.id);
  });

  it("stops the chain when a before command fails", async () => {
    const seen: string[] = [];
    const { runner } = setup(
      async (_, command) => {
        seen.push(command.type);
        return {
          id: command.id,
          success: command.type !== "compile",
          duration: 1,
          error: command.type === "compile" ? "compile_error" : undefined,
          message: command.type === "compile" ? "scripts do not compile" : "ok",
        };
      },
      [
        session({
          capabilities: ["play", "compile"],
          commandChains: { play: { before: ["compile"] } },
        }),
      ],
    );
    const done = await finished(runner, await submit(runner, { type: "play", timeout: 5_000 }));
    expect(seen).toEqual(["compile"]);
    expect(done.result?.success).toBe(false);
    expect(done.result?.error).toBe("compile_error");
    expect(done.result?.message).toBe("compile: scripts do not compile");
  });

  it("fails a chain that calls itself", async () => {
    const { runner } = setup(async (_, command) => ({ id: command.id, success: true, duration: 1 }), [
      session({
        capabilities: ["compile"],
        commandChains: { compile: { before: ["compile"] } },
      }),
    ]);
    const done = await finished(runner, await submit(runner, { type: "compile", timeout: 5_000 }));
    expect(done.status).toBe("failed");
    expect(done.message).toContain("cycles");
  });
});
