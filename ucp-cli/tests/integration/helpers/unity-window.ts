import { spawnSync } from "node:child_process";

/**
 * Minimizes or restores the Unity Editor's main window (Windows only), so the background
 * behaviour is tested without anyone touching the window. Restoring does not activate it.
 */
const USER32 = `Add-Type -Name W -Namespace U -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr h, int c); [DllImport("user32.dll")] public static extern bool IsIconic(System.IntPtr h); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h); [DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);'`;

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

/** The window that has the focus now, to give it back afterwards. */
export function foregroundWindow(): string {
  return powershell(process.pid, "[U.W]::GetForegroundWindow().ToInt64()");
}

/**
 * Brings Unity to the front and gives it the focus. Takes the focus from the user, so only the
 * opt-in foreground test calls it. Windows refuses SetForegroundWindow to a process that is not
 * in the foreground itself; a simulated Alt key press is the documented way past that.
 */
export function focus(pid: number): boolean {
  return (
    powershell(
      pid,
      "[U.W]::ShowWindow($h, 9) | Out-Null; [U.W]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [U.W]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero); [U.W]::SetForegroundWindow($h) | Out-Null; Start-Sleep -Milliseconds 300; ([U.W]::GetForegroundWindow() -eq $h)",
    ) === "True"
  );
}

/** Gives the focus back to a window `foregroundWindow` returned. */
export function refocus(handle: string): void {
  if (!handle || handle === "0") return;
  powershell(
    process.pid,
    `[U.W]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [U.W]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero); [U.W]::SetForegroundWindow([IntPtr]${handle}) | Out-Null`,
  );
}
