[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("fmodel-recreate-sync-tests-" + [Guid]::NewGuid().ToString('N'))
$diagnosticPath = Join-Path $repositoryRoot 'upstream-sync-test-error.txt'
$stage = 'initialization'
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
Remove-Item $diagnosticPath -Force -ErrorAction SilentlyContinue

try {
    $stage = 'FModel-Recreate invariant guard'
    Write-Host "Testing: $stage"
    & (Join-Path $PSScriptRoot 'Assert-RecreateInvariants.ps1') -RepositoryRoot $repositoryRoot

    $stage = 'safe upstream change classification'
    Write-Host "Testing: $stage"
    $safeList = Join-Path $tempRoot 'safe-files.txt'
    Set-Content -LiteralPath $safeList -Value @(
        'FModel/ViewModels/CUE4ParseViewModel.cs',
        'FModel/FModel.csproj'
    ) -Encoding utf8
    $safeJson = & (Join-Path $PSScriptRoot 'Get-UpstreamRisk.ps1') -ChangedFilesPath $safeList
    $safeResult = $safeJson | ConvertFrom-Json
    if ($safeResult.highRisk) { throw 'Safe fixture was incorrectly classified as high risk.' }

    $stage = 'protected upstream change classification'
    Write-Host "Testing: $stage"
    $riskList = Join-Path $tempRoot 'risk-files.txt'
    Set-Content -LiteralPath $riskList -Value @(
        'FModel/App.xaml.cs',
        'FModel/ViewModels/CUE4ParseViewModel.cs'
    ) -Encoding utf8
    $riskJson = & (Join-Path $PSScriptRoot 'Get-UpstreamRisk.ps1') -ChangedFilesPath $riskList
    $riskResult = $riskJson | ConvertFrom-Json
    if (!$riskResult.highRisk) { throw 'Protected-path fixture was not classified as high risk.' }
    if ($riskResult.protectedCount -ne 1) { throw 'Protected-path fixture reported an unexpected match count.' }

    $stage = 'automatic patch version bump'
    Write-Host "Testing: $stage"
    $propsCopy = Join-Path $tempRoot 'Directory.Build.props'
    Copy-Item (Join-Path $repositoryRoot 'Directory.Build.props') $propsCopy
    $before = [regex]::Match((Get-Content $propsCopy -Raw), '<FModelRecreateVersion>(?<v>\d+\.\d+\.\d+)</FModelRecreateVersion>').Groups['v'].Value
    $nextOutput = @(& (Join-Path $PSScriptRoot 'Bump-RecreatePatchVersion.ps1') -PropsPath $propsCopy)
    $next = [string] $nextOutput[-1]
    $after = [regex]::Match((Get-Content $propsCopy -Raw), '<FModelRecreateVersion>(?<v>\d+\.\d+\.\d+)</FModelRecreateVersion>').Groups['v'].Value

    $beforeVersion = [Version] $before
    $expected = [Version]::new($beforeVersion.Major, $beforeVersion.Minor, $beforeVersion.Build + 1).ToString(3)
    if ($next.Trim() -ne $expected -or $after -ne $expected) {
        throw "Patch bump test failed. Expected $expected, output $next, file $after."
    }

    Write-Host 'Upstream sync script tests passed.'
}
catch {
    $details = @(
        "Stage: $stage",
        "Exception: $($_.Exception.GetType().FullName)",
        "Message: $($_.Exception.Message)",
        "Script stack trace:",
        $_.ScriptStackTrace,
        "Full error:",
        ($_ | Format-List * -Force | Out-String)
    )
    Set-Content -LiteralPath $diagnosticPath -Value $details -Encoding utf8
    Get-Content -LiteralPath $diagnosticPath
    throw
}
finally {
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
