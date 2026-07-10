# AIRGAP Phase 0 Part D smoke test:
# two separate processes connect over loopback UDP, each resolves into the correct
# role, and a ping RPC round-trips. Run after AIRGAP.CI.Build.WindowsPlayer.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-test.ps1
#
# Exits 0 on PASS, 1 on FAIL.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'Builds\Windows\Airgap.exe'
$logDir = Join-Path $root 'Logs'
$hostLog = Join-Path $logDir 'smoke-host.log'
$clientLog = Join-Path $logDir 'smoke-client.log'

if (-not (Test-Path $exe)) {
    Write-Host "FAIL: player build not found at $exe (run AIRGAP.CI.Build.WindowsPlayer first)"
    exit 1
}
New-Item -ItemType Directory -Force $logDir | Out-Null
Remove-Item $hostLog, $clientLog -Force -ErrorAction SilentlyContinue

Write-Host 'Starting host (Infiltrator)...'
$hostProc = Start-Process -FilePath $exe -PassThru -ArgumentList @(
    '-batchmode', '-nographics',
    '-airgap-role', 'host',
    '-airgap-playerrole', 'infiltrator',
    '-airgap-smoke',
    '-logFile', $hostLog
)

Start-Sleep -Seconds 8

Write-Host 'Starting client...'
$clientProc = Start-Process -FilePath $exe -PassThru -ArgumentList @(
    '-batchmode', '-nographics',
    '-airgap-role', 'client',
    '-airgap-ip', '127.0.0.1',
    '-airgap-smoke',
    '-logFile', $clientLog
)

if (-not $clientProc.WaitForExit(120000)) {
    Write-Host 'FAIL: client did not exit within 120s'
    try { $clientProc.Kill() } catch {}
    try { $hostProc.Kill() } catch {}
    exit 1
}
if (-not $hostProc.WaitForExit(60000)) {
    Write-Host 'FAIL: host did not exit within 60s of client exiting'
    try { $hostProc.Kill() } catch {}
    exit 1
}

$failures = @()
if ($clientProc.ExitCode -ne 0) { $failures += "client exit code $($clientProc.ExitCode)" }
if ($hostProc.ExitCode -ne 0) { $failures += "host exit code $($hostProc.ExitCode)" }

$hostText = Get-Content $hostLog -Raw
$clientText = Get-Content $clientLog -Raw

if ($hostText -notmatch '\[AIRGAP\] SMOKE ping received') { $failures += 'host never logged the ping' }
if ($hostText -notmatch '\[AIRGAP\] ROLE assigned: Infiltrator') { $failures += 'host never resolved its Infiltrator role' }
if ($clientText -notmatch '\[AIRGAP\] SMOKE pong received rtt=') { $failures += 'client never logged the pong' }
if ($clientText -notmatch '\[AIRGAP\] ROLE assigned: Warden') { $failures += 'client never resolved its Warden role' }

if ($failures.Count -eq 0) {
    $rtt = [regex]::Match($clientText, 'rtt=([\d.]+)ms').Groups[1].Value
    Write-Host "PASS: two processes connected, roles resolved (host=Infiltrator, client=Warden), ping round-tripped (rtt=${rtt}ms)"
    exit 0
} else {
    foreach ($f in $failures) { Write-Host "FAIL: $f" }
    Write-Host "Logs: $hostLog / $clientLog"
    exit 1
}
