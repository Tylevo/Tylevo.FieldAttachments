[CmdletBinding()]
param([string]$SptPath, [switch]$Install)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Repo = Split-Path -Parent $PSScriptRoot
$Out = Join-Path $Repo 'dist'
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Log = Join-Path $Out 'build.log'
Set-Content -LiteralPath $Log -Encoding UTF8 -Value ('Field Attachments build started ' + (Get-Date).ToUniversalTime().ToString('o'))
$OldLocation = Get-Location
$OldRoot = [Environment]::GetEnvironmentVariable('TFA_SPT_ROOT', 'Process')
function Run-DotNet([string[]]$Arguments) {
    Write-Host ('dotnet ' + ($Arguments -join ' '))
    Add-Content -LiteralPath $Log -Value ('dotnet ' + ($Arguments -join ' '))
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & $script:DotNet @Arguments 2>&1 | ForEach-Object { $s = $_.ToString(); Write-Host $s; Add-Content -LiteralPath $Log -Value $s }
    $code = $LASTEXITCODE
    $ErrorActionPreference = $old
    if ($code -ne 0) { throw ('dotnet exited with code ' + $code + '. See dist\build.log.') }
}
try {
    Set-Location -LiteralPath $Repo
    if ([string]::IsNullOrWhiteSpace($SptPath)) { $SptPath = Read-Host 'SPT GAME folder (contains EscapeFromTarkov.exe; no trailing quotes)' }
    $SptPath = $SptPath.Trim().Trim('"')
    if ($SptPath.Contains(';')) { throw 'A semicolon in the game path is not supported by this MSBuild helper.' }
    $SptPath = (Resolve-Path -LiteralPath $SptPath).Path
    $Managed = Join-Path $SptPath 'EscapeFromTarkov_Data\Managed'
    if (!(Test-Path -LiteralPath (Join-Path $SptPath 'EscapeFromTarkov.exe') -PathType Leaf)) { throw 'That is not the SPT game folder.' }
    $Required = @('BepInEx\core\BepInEx.dll', 'BepInEx\core\0Harmony.dll', 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll',
        'EscapeFromTarkov_Data\Managed\Sirenix.Serialization.dll',
        'BepInEx\plugins\UnityToolkit\UnityToolkit.dll', 'BepInEx\plugins\UnityToolkit\UniTask.dll',
        'EscapeFromTarkov_Data\Managed\UnityEngine.dll', 'EscapeFromTarkov_Data\Managed\UnityEngine.CoreModule.dll',
        'EscapeFromTarkov_Data\Managed\UnityEngine.InputLegacyModule.dll', 'EscapeFromTarkov_Data\Managed\UnityEngine.TextRenderingModule.dll',
        'EscapeFromTarkov_Data\Managed\UnityEngine.AnimationModule.dll',
        'EscapeFromTarkov_Data\Managed\UnityEngine.AssetBundleModule.dll',
        'EscapeFromTarkov_Data\Managed\UnityEngine.UI.dll', 'EscapeFromTarkov_Data\Managed\UnityEngine.UIModule.dll')
    foreach ($relative in $Required) {
        if (!(Test-Path -LiteralPath (Join-Path $SptPath $relative) -PathType Leaf)) { throw ('Missing local reference: ' + $relative) }
    }
    $ClientCompatibility = & (Join-Path $PSScriptRoot 'Test-ClientCompatibility.ps1') -SptPath $SptPath
    $CompatibilityNotice = 'Verified SPT ' + $ClientCompatibility.actualSptVersion + ' with the inspected native game assembly; runtime gates retained.'
    Write-Host $CompatibilityNotice
    Add-Content -LiteralPath $Log -Value $CompatibilityNotice
    $Command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $Command) { throw 'Install the Microsoft .NET SDK (8 or newer), not just a runtime, then reopen a terminal. No Codex or Unity Editor is required.' }
    $script:DotNet = $Command.Source
    $sdks = & $script:DotNet --list-sdks
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($sdks -join ''))) { throw 'No .NET SDK is installed. A server runtime alone cannot compile this project.' }
    $version = (& $script:DotNet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^(\d+)\.') { throw 'Could not determine selected .NET SDK version.' }
    $major = [int]$Matches[1]
    if ($major -lt 8) { throw 'This build helper requires SDK 8 or newer.' }
    [Environment]::SetEnvironmentVariable('TFA_SPT_ROOT', $SptPath, 'Process')
    $runtime = 'net' + $major + '.0'
    Run-DotNet -Arguments @('build', (Join-Path $Repo 'tests\FieldAttachments.Tests.csproj'), '-c', 'Release', '--verbosity', 'minimal', ('-p:RuntimeTargetFramework=' + $runtime))
    Run-DotNet -Arguments @((Join-Path $Repo ('tests\bin\Release\' + $runtime + '\FieldAttachments.Tests.dll')))
    Run-DotNet -Arguments @('build', (Join-Path $Repo 'src\Tylevo.FieldAttachments.csproj'), '-c', 'Release', '--verbosity', 'minimal')
    $Dll = Join-Path $Repo 'src\bin\Release\Tylevo.FieldAttachments.dll'
    if (!(Test-Path -LiteralPath $Dll)) { throw 'Compiler did not produce the expected plugin DLL.' }
    $AnimationHashes = [ordered]@{}
    $AnimationPins = [ordered]@{ stm='BundleHash'; m4a1='M4BundleHash'; mcx='McxBundleHash'; m700='M700BundleHash'; m870='M870BundleHash'; glock17='Glock17BundleHash' }
    $AnimationSource = Get-Content -Raw -LiteralPath (Join-Path $Repo 'src\Runtime\CustomStmClip.cs')
    foreach ($name in $AnimationPins.Keys) {
        $relative = 'Animation/' + $name + '_presentation.bundle'
        $bundle = Join-Path $Repo ('assets/' + $relative)
        $hash = [regex]::Match($AnimationSource, ('\b' + $AnimationPins[$name] + ' = "([A-F0-9]{64})"')).Groups[1].Value
        if ($hash.Length -ne 64 -or !(Test-Path -LiteralPath $bundle) -or (Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash -ne $hash) {
            throw ('Authored animation missing or differs from tested export: ' + $relative)
        }
        $AnimationHashes[$relative] = $hash
    }
    $AnimationHash = $AnimationHashes['Animation/stm_presentation.bundle']
    $FamilyManifest = Get-Content -LiteralPath (Join-Path $Repo 'assets/Animation/family-presentations.json') -Raw | ConvertFrom-Json
    if ($FamilyManifest.schema -ne 1 -or $FamilyManifest.unityVersion -ne '2022.3.43f1') { throw 'Unknown family animation manifest.' }
    foreach ($entry in $FamilyManifest.recipients) {
        if ($entry.bundle -notmatch '^[a-z0-9_]+_presentation$' -or $entry.sha256 -notmatch '^[A-F0-9]{64}$') { throw 'Invalid family asset identifier/hash.' }
        $relative = 'Animation/' + $entry.bundle + '.bundle'
        if ($AnimationHashes.Contains($relative) -or (Get-FileHash -LiteralPath (Join-Path $Repo ('assets/' + $relative))).Hash -ne $entry.sha256) { throw ('Duplicate or changed family animation: ' + $relative) }
        $AnimationHashes[$relative] = $entry.sha256
    }
    $BroadManifest = Get-Content -LiteralPath (Join-Path $Repo 'assets/Animation/all-presentations.json') -Raw | ConvertFrom-Json
    if ($BroadManifest.schema -ne 1 -or $BroadManifest.presentation -ne 'MCX-angle/STM-withdrawn-arm-v1') { throw 'Unknown broad animation manifest.' }
    $BroadHashes = @{}
    foreach ($entry in $BroadManifest.recipients) {
        if ($entry.bundle -notmatch '^gun_[a-f0-9]{24}_presentation$' -or $entry.sha256 -notmatch '^[A-F0-9]{64}$') { throw 'Invalid broad animation identifier/hash.' }
        $relative = 'Animation/' + $entry.bundle + '.bundle'
        if ($BroadHashes.ContainsKey($relative)) {
            if ($BroadHashes[$relative] -ne $entry.sha256) { throw 'Conflicting alias asset hash.' }
            continue
        }
        if ($AnimationHashes.Contains($relative) -or (Get-FileHash -LiteralPath (Join-Path $Repo ('assets/' + $relative))).Hash -ne $entry.sha256) { throw ('Changed broad animation: ' + $relative) }
        $BroadHashes[$relative] = $entry.sha256
        $AnimationHashes[$relative] = $entry.sha256
    }
    $AdditionalManifest = Get-Content -LiteralPath (Join-Path $Repo 'assets/Animation/additional-presentations.json') -Raw | ConvertFrom-Json
    if ($AdditionalManifest.schema -ne 1 -or $AdditionalManifest.presentation -ne 'MCX-angle/STM-withdrawn-arm-v1' -or
        @($AdditionalManifest.recipients).Count -ne 6 -or @($AdditionalManifest.aliases).Count -ne 26) { throw 'Unknown additional animation manifest.' }
    foreach ($entry in $AdditionalManifest.recipients) {
        if ($entry.bundle -notmatch '^gun_[a-f0-9]{24}_presentation$' -or $entry.sha256 -notmatch '^[A-F0-9]{64}$') { throw 'Invalid additional animation identifier/hash.' }
        $relative = 'Animation/' + $entry.bundle + '.bundle'
        if ($AnimationHashes.Contains($relative) -or (Get-FileHash -LiteralPath (Join-Path $Repo ('assets/' + $relative))).Hash -ne $entry.sha256) { throw ('Duplicate or changed additional animation: ' + $relative) }
        $AnimationHashes[$relative] = $entry.sha256
    }
    if ($AnimationHashes.Count -ne 129) { throw 'Expected all 123 original and six additional authored animations.' }
    $AnimationBaseline = Get-Content -LiteralPath (Join-Path $Repo 'tools/animation-baseline-0.25.1.json') -Raw | ConvertFrom-Json
    $BaselinePins = @($AnimationBaseline.authoredAnimationHashes.PSObject.Properties)
    if ($AnimationBaseline.schema -ne 1 -or $AnimationBaseline.baselineVersion -ne '0.25.1' -or $BaselinePins.Count -ne 123) { throw 'Unknown original animation baseline.' }
    foreach ($pin in $BaselinePins) {
        if (!$AnimationHashes.Contains($pin.Name) -or $AnimationHashes[$pin.Name] -ne $pin.Value) { throw ('Original animation baseline changed: ' + $pin.Name) }
    }
    $PackageDir = Join-Path $Out 'BepInEx\plugins\Tylevo.FieldAttachments'
    New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null
    Copy-Item -LiteralPath $Dll -Destination $PackageDir -Force
    $AnimationDestination = Join-Path $PackageDir 'Animation'
    New-Item -ItemType Directory -Force -Path $AnimationDestination | Out-Null
    foreach ($relative in $AnimationHashes.Keys) { Copy-Item -LiteralPath (Join-Path $Repo ('assets/' + $relative)) -Destination $AnimationDestination -Force }
    Copy-Item -LiteralPath (Join-Path $Repo 'README.md') -Destination $PackageDir -Force
    Copy-Item -LiteralPath (Join-Path $Repo 'LICENSE') -Destination $PackageDir -Force
    $SptPatchReferences = @('BepInEx\plugins\spt\spt-core.dll', 'BepInEx\plugins\spt\spt-common.dll',
        'BepInEx\plugins\spt\spt-reflection.dll', 'BepInEx\plugins\spt\spt-singleplayer.dll',
        'BepInEx\plugins\spt\spt-custom.dll', 'BepInEx\plugins\spt\spt-debugging.dll') |
        Where-Object { Test-Path -LiteralPath (Join-Path $SptPath $_) -PathType Leaf }
    $References = foreach ($relative in @($Required) + @($SptPatchReferences)) {
        $file = Join-Path $SptPath $relative
        [PSCustomObject]@{ relativePath = $relative; bytes = (Get-Item -LiteralPath $file).Length;
            sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash;
            fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($file).FileVersion }
    }
    $Manifest = [ordered]@{ pluginVersion = '1.0.0'; altMenuHoldAndToggle = $true; original025CardLayout = $true; readOnlyByDefault = $false; experimentalEmptySlotInstallPresent = $true; nativeTacticalUninstallPresent = $true; nativeLeafAttachmentCategories = @('Optic','Muzzle','Tactical','Underbarrel'); clickActionsPresent = $true; swapsEnabled = $true; nativeAtomicSwapUsed = $false; sequentialReplacement = $true; requestedTarget = ('SPT ' + $ClientCompatibility.actualSptVersion); supportedTargets = @('SPT 4.1.5', 'SPT 4.1.6'); actualSptVersion = $ClientCompatibility.actualSptVersion; clientCompatibility = $ClientCompatibility; gameVersionMustBeVerified = $true;
        createdUtc = (Get-Date).ToUniversalTime().ToString('o'); sdk = $version;
        runtimeTestsPerformed = $false; coreTestsPassed = $true; customStmAnimationSha256 = $AnimationHash; authoredAnimationHashes = $AnimationHashes;
        authoredTemplateCount = 194; additionalAliasCount = 26; original123AnimationHashesPreserved = $true;
        pluginSha256 = (Get-FileHash -LiteralPath $Dll -Algorithm SHA256).Hash; references = @($References) }
    $Manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PackageDir 'build-manifest.json') -Encoding UTF8
    Write-Host "`nBuild and core tests succeeded. This is NOT proof of in-game compatibility."
    Write-Host ('Ready to copy: ' + (Join-Path $Out 'BepInEx'))
    if ($Install) {
        Write-Host ('Destination: ' + (Join-Path $SptPath 'BepInEx\plugins\Tylevo.FieldAttachments'))
        $answer = Read-Host 'Copy this plugin (attachment changes enabled by default) into that installation? [y/N]'
        if ($answer -match '^(y|yes)$') {
            if (Get-Process -Name EscapeFromTarkov -ErrorAction SilentlyContinue) { throw 'Close EscapeFromTarkov before installing. Built files are still in dist.' }
            $Destination = Join-Path $SptPath 'BepInEx\plugins\Tylevo.FieldAttachments'
            New-Item -ItemType Directory -Path $Destination -Force | Out-Null
            $Existing = Join-Path $Destination 'Tylevo.FieldAttachments.dll'
            if (Test-Path -LiteralPath $Existing) {
                $Backup = Join-Path $Repo 'backups'
                New-Item -ItemType Directory -Path $Backup -Force | Out-Null
                Copy-Item -LiteralPath $Existing -Destination (Join-Path $Backup ('Tylevo.FieldAttachments-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.dll.bak'))
            }
            $AssetBackup = Join-Path $Repo ('backups/authored-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
            foreach ($file in @('Tylevo.FieldAttachments.dll', 'README.md', 'LICENSE', 'build-manifest.json') + @($AnimationHashes.Keys)) {
                $target = Join-Path $Destination $file
                if (Test-Path -LiteralPath $target) {
                    $saved = Join-Path $AssetBackup $file
                    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $saved) | Out-Null
                    Copy-Item -LiteralPath $target -Destination $saved
                }
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
                Copy-Item -LiteralPath (Join-Path $PackageDir $file) -Destination $target -Force
                if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PackageDir $file)).Hash) { throw ('Installed hash mismatch: ' + $file) }
            }
            Write-Host 'Installed. Hold Left Alt for attachment mode and cursor; choose Hold or Toggle in F12.'
        } else { Write-Host 'Not installed. Copy dist\BepInEx manually when ready.' }
    }
} catch {
    Add-Content -LiteralPath $Log -Value $_.ToString()
    Write-Host ('BUILD STOPPED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host ('Send this file if needed: ' + $Log)
    exit 1
} finally {
    [Environment]::SetEnvironmentVariable('TFA_SPT_ROOT', $OldRoot, 'Process')
    Set-Location -LiteralPath $OldLocation.Path
}
