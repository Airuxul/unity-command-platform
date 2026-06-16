# One-time setup for ucp-cli (ucp-cli + ucp-host)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

npm unlink -g uctl 2>$null | Out-Null
npm unlink -g unity-cmd 2>$null | Out-Null
npm unlink -g ucp-cli 2>$null | Out-Null
npm unlink -g ucp-host 2>$null | Out-Null

# Remove stale global shims left by old unity-cmd / partial link (npm link EEXIST).
$npmBin = Join-Path $env:APPDATA "npm"
foreach ($name in @("ucp-cli", "ucp-host", "uctl", "unity-cmd")) {
  foreach ($ext in @("", ".cmd", ".ps1")) {
    $shim = Join-Path $npmBin ($name + $ext)
    if (Test-Path $shim) {
      Remove-Item $shim -Force
    }
  }
}

npm install
npm run build
npm link

Write-Host ""
Write-Host "Done. Run commands:" -ForegroundColor Green
Write-Host "  node .\ucp-cli ping         (local, no global link)"
Write-Host "  ucp-cli ping                (global, after npm link above)"
Write-Host "  npm run ucp-cli -- ping     (auto-builds via preucp-cli)"
Write-Host ""
