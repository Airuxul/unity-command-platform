import { OUTBOX_POLL_INTERVAL_MS } from "#shared/index.js";
import type { UcpResult } from "#shared/types.js";

export function retryBackoffMs(attempt: number): number {
  return 500 * 2 ** Math.max(0, attempt - 1);
}

export interface RetryPolicyOptions {
  maxAttempts?: number;
}

export class RetryPolicy {
  readonly maxAttempts: number;

  constructor(options: RetryPolicyOptions = {}) {
    this.maxAttempts = options.maxAttempts ?? 2;
  }

  shouldRetry(attempts: number): boolean {
    return attempts < this.maxAttempts;
  }

  async waitBeforeRetry(attempts: number): Promise<void> {
    const delay = retryBackoffMs(attempts);
    await new Promise((r) => setTimeout(r, delay));
  }

  async executeWithTimeout(timeoutMs: number, fn: () => Promise<UcpResult>): Promise<UcpResult> {
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      return await Promise.race([
        fn(),
        new Promise<UcpResult>((_, reject) => {
          timer = setTimeout(() => reject(new Error("timeout")), timeoutMs);
        }),
      ]);
    } finally {
      if (timer) clearTimeout(timer);
    }
  }
}

export { OUTBOX_POLL_INTERVAL_MS };
