[CmdletBinding()]
param(
    [string] $PropsPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path 'Directory.Build.props')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (!(Test-Path -LiteralPath $PropsPath)) {
    throw "Version manifest was not found: $PropsPath"
}

$content = Get-Content -LiteralPath $PropsPath -Raw
$match = [regex]::Match($content, '<FModelRecreateVersion>(?<version>\d+\.\d+\.\d+)</FModelRecreateVersion>')
if (!$match.Success) {
    throw 'FModelRecreateVersion was not found or is not a three-part semantic version.'
}

$current = [Version] $match.Groups['version'].Value
$next = [Version]::new($current.Major, $current.Minor, $current.Build + 1)
$nextText = $next.ToString(3)

$content = [regex]::Replace(
    $content,
    '<FModelRecreateVersion>\d+\.\d+\.\d+</FModelRecreateVersion>',
    "<FModelRecreateVersion>$nextText</FModelRecreateVersion>",
    1)

if ($content -match '<FModelRecreateReleaseRevision>\d+</FModelRecreateReleaseRevision>') {
    $content = [regex]::Replace(
        $content,
        '<FModelRecreateReleaseRevision>\d+</FModelRecreateReleaseRevision>',
        '<FModelRecreateReleaseRevision>0</FModelRecreateReleaseRevision>',
        1)
}

Set-Content -LiteralPath $PropsPath -Value $content -Encoding utf8 -NoNewline
$nextText
