import type { LogLevel } from "#shared/types.js";

export class Logger {
  private readonly minIndex: number;

  constructor(options: { level?: LogLevel } = {}) {
    const level = options.level ?? "info";
    const levels: LogLevel[] = ["debug", "info", "warn", "error"];
    this.minIndex = levels.indexOf(level);
  }

  #shouldLog(level: LogLevel): boolean {
    const levels: LogLevel[] = ["debug", "info", "warn", "error"];
    return levels.indexOf(level) >= this.minIndex;
  }

  debug(msg: string, meta?: Record<string, unknown>): void {
    if (this.#shouldLog("debug")) this.#emit("debug", msg, meta);
  }

  info(msg: string, meta?: Record<string, unknown>): void {
    if (this.#shouldLog("info")) this.#emit("info", msg, meta);
  }

  warn(msg: string, meta?: Record<string, unknown>): void {
    if (this.#shouldLog("warn")) this.#emit("warn", msg, meta);
  }

  error(msg: string, meta?: Record<string, unknown>): void {
    if (this.#shouldLog("error")) this.#emit("error", msg, meta);
  }

  #emit(level: LogLevel, msg: string, meta?: Record<string, unknown>): void {
    const line = meta ? `${msg} ${JSON.stringify(meta)}` : msg;
    const fn = level === "error" ? console.error : level === "warn" ? console.warn : console.log;
    fn(`[ucp-host:${level}] ${line}`);
  }
}
