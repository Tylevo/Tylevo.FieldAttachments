[CmdletBinding()]
param([string]$SptPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Repo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SptPath)) { $SptPath = Read-Host 'SPT game folder' }
$SptPath = (Resolve-Path -LiteralPath $SptPath.Trim().Trim('"')).Path
$PluginRoot = Join-Path $SptPath 'BepInEx\plugins\Tylevo.FieldAttachments'
$Reports = Join-Path $PluginRoot 'Reports'
$Latest = if (Test-Path -LiteralPath $Reports) {
    Get-ChildItem -LiteralPath $Reports -Directory | Where-Object { !($_.Attributes -band [IO.FileAttributes]::ReparsePoint) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
} else { $null }
$Files = @()
if ($null -ne $Latest) {
    foreach ($name in @('ABOUT.txt','snapshot.json','scan-trace.txt','install-probe.txt','ui-layout.txt','inspection-pose.txt','bindings.txt','runtime.txt','api-map.txt')) {
        $p = Join-Path $Latest.FullName $name
        if (Test-Path -LiteralPath $p -PathType Leaf) { $Files += $p }
    }
}
$ProbeLogs = Join-Path $PluginRoot 'ProbeLogs'
$Journal = if (Test-Path -LiteralPath $ProbeLogs) {
    Get-ChildItem -LiteralPath $ProbeLogs -Filter 'session-*.txt' -File |
        Where-Object { !($_.Attributes -band [IO.FileAttributes]::ReparsePoint) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
} else { $null }
if ($null -ne $Journal) { $Files += $Journal.FullName }
if ($Files.Count -eq 0) { throw 'No F10 report or automatic session journal found. Open the overlay and reproduce the input once.' }
$manifest = Join-Path $PluginRoot 'build-manifest.json'
if (Test-Path -LiteralPath $manifest) { $Files += $manifest }
$Stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
$Scratch = Join-Path ([IO.Path]::GetTempPath()) ('tfa-feedback-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Scratch | Out-Null
$Index = Join-Path $Scratch 'feedback-index.txt'
try {
    $SnapshotTime = 'none'
    if ($null -ne $Latest -and (Test-Path -LiteralPath (Join-Path $Latest.FullName 'snapshot.json'))) {
        $Snapshot = Get-Content -LiteralPath (Join-Path $Latest.FullName 'snapshot.json') -Raw | ConvertFrom-Json
        $SnapshotTime = $Snapshot.capturedUtc
    }
    @(
        ('Collected UTC: ' + (Get-Date).ToUniversalTime().ToString('o')),
        ('F10 snapshot UTC: ' + $SnapshotTime),
        ('Automatic event journal: ' + $(if ($null -ne $Journal) { $Journal.Name } else { 'none (older plugin or no input events)' })),
        ('Journal last-write UTC: ' + $(if ($null -ne $Journal) { $Journal.LastWriteTimeUtc.ToString('o') } else { 'none' })),
        'The automatic journal can be NEWER than the F10 snapshot. Do not treat the snapshot as the state at a later request.',
        'Journal contains the last 512 input/state/request/completion events from one plugin session, with UTC timestamps.',
        'No full BepInEx log, profile, config or game DLL is included.'
    ) | Set-Content -LiteralPath $Index -Encoding UTF8
    $Files += $Index
    $Destination = Join-Path $Repo ('FieldAttachments-Feedback-' + $Stamp + '.zip')
    Compress-Archive -LiteralPath $Files -DestinationPath $Destination -CompressionLevel Optimal
} finally {
    if (Test-Path -LiteralPath $Index) { Remove-Item -LiteralPath $Index }
    Remove-Item -LiteralPath $Scratch # Empty task-owned directory only; no recursive deletion.
}
Write-Host ('Created: ' + $Destination)
Write-Host 'Review feedback-index.txt first: event journal and F10 snapshot may have different timestamps. No profile files, config, game DLLs or full logs.'
