[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("fmodel-recreate-sync-tests-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    & (Join-Path $PSScriptRoot 'Assert-RecreateInvariants.ps1') -RepositoryRoot $repositoryRoot

    $safeList = Join-Path $tempRoot 'safe-files.txt'
    Set-Content -LiteralPath $safeList -Value @(
        'FModel/ViewModels/CUE4ParseViewModel.cs',
        'FModel/FModel.csproj'
    ) -Encoding utf8
    $safeResult = & (Join-Path $PSScriptRoot 'Get-UpstreamRisk.ps1') -ChangedFilesPath $safeList | ConvertFrom-Json
    if ($safeResult.highRisk) { throw 'Safe fixture was incorrectly classified as high risk.' }

    $riskList = Join-Path $tempRoot 'risk-files.txt'
    Set-Content -LiteralPath $riskList -Value @(
        'FModel/App.xaml.cs',
        'FModel/ViewModels/CUE4ParseViewModel.cs'
    ) -Encoding utf8
    $riskResult = & (Join-Path $PSScriptRoot 'Get-UpstreamRisk.ps1') -ChangedFilesPath $riskList | ConvertFrom-Json
    if (!$riskResult.highRisk) { throw 'Protected-path fixture was not classified as high risk.' }
    if ($riskResult.protectedCount -ne 1) { throw 'Protected-path fixture reported an unexpected match count.' }

    $propsCopy = Join-Path $tempRoot 'Directory.Build.props'
    Copy-Item (Join-Path $repositoryRoot 'Directory.Build.props') $propsCopy
    $before = [regex]::Match((Get-Content $propsCopy -Raw), '<FModelRecreateVersion>(?<v>\d+\.\d+\.\d+)</FModelRecreateVersion>').Groups['v'].Value
    $next = & (Join-Path $PSScriptRoot 'Bump-RecreatePatchVersion.ps1') -PropsPath $propsCopy
    $after = [regex]::Match((Get-Content $propsCopy -Raw), '<FModelRecreateVersion>(?<v>\d+\.\d+\.\d+)</FModelRecreateVersion>').Groups['v'].Value

    $beforeVersion = [Version] $before
    $expected = [Version]::new($beforeVersion.Major, $beforeVersion.Minor, $beforeVersion.Build + 1).ToString(3)
    if ($next.Trim() -ne $expected -or $after -ne $expected) {
        throw "Patch bump test failed. Expected $expected, output $next, file $after."
    }

    Write-Host 'Upstream sync script tests passed.'
}
finally {
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
