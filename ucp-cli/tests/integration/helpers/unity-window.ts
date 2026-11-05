import { spawnSync } from "node:child_process";

/**
 * Minimizes or restores the Unity Editor's main window (Windows only), so the background
 * behaviour is tested without anyone touching the window. Restoring does not activate it.
 */
const USER32 = `Add-Type -Name W -Namespace U -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr h, int c); [DllImport("user32.dll")] public static extern bool IsIconic(System.IntPtr h);'`;

function powershell(pid: number, body: string): string {
  const script = `${USER32}; $h = (Get-Process -Id ${pid}).MainWindowHandle; ${body}`;
  const res = spawnSync(
    "powershell",
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script],
    {
      encoding: "utf8",
    },
  );
  if (res.status !== 0) throw new Error(`powershell failed: ${res.stderr}`);
  return res.stdout.trim();
}

export function isMinimized(pid: number): boolean {
  return powershell(pid, "[U.W]::IsIconic($h)") === "True";
}

/** SW_MINIMIZE. */
export function minimize(pid: number): void {
  powershell(pid, "[U.W]::ShowWindow($h, 6) | Out-Null");
}

/** SW_SHOWNOACTIVATE: back to its size without taking the focus from whatever has it. */
export function restore(pid: number): void {
  powershell(pid, "[U.W]::ShowWindow($h, 4) | Out-Null");
}
