[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ChangedFilesPath,

    [string] $ProtectedPathsPath = (Join-Path $PSScriptRoot 'protected-paths.txt'),

    [string] $ReportPath,

    [string] $GitHubOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (!(Test-Path -LiteralPath $ChangedFilesPath)) {
    throw "Changed-files list was not found: $ChangedFilesPath"
}

if (!(Test-Path -LiteralPath $ProtectedPathsPath)) {
    throw "Protected-path policy was not found: $ProtectedPathsPath"
}

$changedFiles = @(
    Get-Content -LiteralPath $ChangedFilesPath |
        ForEach-Object { $_.Trim().Replace('\\', '/') } |
        Where-Object { $_ }
)

$protectedPatterns = @(
    Get-Content -LiteralPath $ProtectedPathsPath |
        ForEach-Object { $_.Trim().Replace('\\', '/') } |
        Where-Object { $_ -and !$_.StartsWith('#') }
)

$protectedMatches = [System.Collections.Generic.List[string]]::new()
foreach ($file in $changedFiles) {
    foreach ($pattern in $protectedPatterns) {
        if ($file -like $pattern) {
            [void] $protectedMatches.Add($file)
            break
        }
    }
}

$protectedMatches = @($protectedMatches | Sort-Object -Unique)
$largeChange = $changedFiles.Count -gt 150
$highRisk = $protectedMatches.Count -gt 0 -or $largeChange

$reasons = [System.Collections.Generic.List[string]]::new()
if ($protectedMatches.Count -gt 0) {
    [void] $reasons.Add("Upstream modifies $($protectedMatches.Count) protected FModel-Recreate path(s).")
}
if ($largeChange) {
    [void] $reasons.Add("Upstream changes $($changedFiles.Count) files, exceeding the 150-file automatic-merge limit.")
}
if ($reasons.Count -eq 0) {
    [void] $reasons.Add('No protected paths or oversized change set detected.')
}

if ($ReportPath) {
    $report = [System.Collections.Generic.List[string]]::new()
    [void] $report.Add('# Upstream sync risk report')
    [void] $report.Add('')
    [void] $report.Add("- Changed files: $($changedFiles.Count)")
    [void] $report.Add("- Protected-path matches: $($protectedMatches.Count)")
    [void] $report.Add("- High risk: $($highRisk.ToString().ToLowerInvariant())")
    [void] $report.Add('')
    [void] $report.Add('## Reasons')
    foreach ($reason in $reasons) { [void] $report.Add("- $reason") }

    if ($protectedMatches.Count -gt 0) {
        [void] $report.Add('')
        [void] $report.Add('## Protected paths touched')
        foreach ($match in $protectedMatches) { [void] $report.Add(('- `{0}`' -f $match)) }
    }

    $reportDirectory = Split-Path -Parent $ReportPath
    if ($reportDirectory) { New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null }
    Set-Content -LiteralPath $ReportPath -Value $report -Encoding utf8
}

if ($GitHubOutput) {
    "high_risk=$($highRisk.ToString().ToLowerInvariant())" | Add-Content -LiteralPath $GitHubOutput
    "changed_count=$($changedFiles.Count)" | Add-Content -LiteralPath $GitHubOutput
    "protected_count=$($protectedMatches.Count)" | Add-Content -LiteralPath $GitHubOutput
}

[pscustomobject]@{
    highRisk = $highRisk
    changedCount = $changedFiles.Count
    protectedCount = $protectedMatches.Count
    protectedMatches = $protectedMatches
    reasons = @($reasons)
} | ConvertTo-Json -Depth 5 -Compress
