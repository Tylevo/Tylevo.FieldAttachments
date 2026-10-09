[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$SptPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path -LiteralPath $SptPath).Path
$CorePath = Join-Path $Root 'BepInEx\plugins\spt\spt-core.dll'
$GamePath = Join-Path $Root 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll'
foreach ($path in @($CorePath, $GamePath)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Missing compatibility reference: ' + $path) }
}

# GetAssemblyName reads the PE metadata without loading or executing the game/plugin.
$CoreIdentity = [Reflection.AssemblyName]::GetAssemblyName($CorePath)
$CoreVersion = $CoreIdentity.Version.ToString()
$SupportedVersions = @('4.1.5.0', '4.1.6.0')
if ($CoreIdentity.Name -ne 'spt-core' -or $CoreVersion -notin $SupportedVersions) {
    throw ('Unsupported SPT.Core identity: ' + $CoreIdentity.Name + ' ' + $CoreVersion + '. Expected SPT 4.1.5 or 4.1.6.')
}
$ExpectedGameSha256 = 'EE25CEE1259777B38ED8B3E7841FDC2DB3C98540B1469FA539B1FF183476E436'
$GameSha256 = (Get-FileHash -LiteralPath $GamePath -Algorithm SHA256).Hash
if ($GameSha256 -ne $ExpectedGameSha256) {
    throw ('Game assembly differs from the inspected EFT 40743 binary. Expected SHA-256 ' + $ExpectedGameSha256 + '; found ' + $GameSha256 + '. Runtime gates must not be relaxed.')
}

[PSCustomObject][ordered]@{
    actualSptVersion = $CoreIdentity.Version.ToString(3)
    sptCoreAssemblyVersion = $CoreVersion
    sptCoreSha256 = (Get-FileHash -LiteralPath $CorePath -Algorithm SHA256).Hash
    gameAssemblySha256 = $GameSha256
    expectedGameMvid = 'cc2d80b0-6d5b-4cb1-a581-6d2cc901d4c7'
    verification = 'SPT.Core metadata and exact game assembly SHA-256; no game code executed'
}
