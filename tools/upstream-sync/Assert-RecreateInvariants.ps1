[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()

function Get-RequiredContent {
    param([Parameter(Mandatory)][string] $RelativePath)

    $fullPath = Join-Path $RepositoryRoot $RelativePath
    if (!(Test-Path -LiteralPath $fullPath)) {
        $failures.Add("Required file is missing: $RelativePath")
        return $null
    }

    return Get-Content -LiteralPath $fullPath -Raw
}

function Require-Pattern {
    param(
        [Parameter(Mandatory)][string] $RelativePath,
        [Parameter(Mandatory)][string] $Pattern,
        [Parameter(Mandatory)][string] $Description
    )

    $content = Get-RequiredContent -RelativePath $RelativePath
    if ($null -ne $content -and $content -notmatch $Pattern) {
        $failures.Add("$RelativePath no longer preserves: $Description")
    }
}

function Forbid-Pattern {
    param(
        [Parameter(Mandatory)][string] $RelativePath,
        [Parameter(Mandatory)][string] $Pattern,
        [Parameter(Mandatory)][string] $Description
    )

    $content = Get-RequiredContent -RelativePath $RelativePath
    if ($null -ne $content -and $content -match $Pattern) {
        $failures.Add("$RelativePath contains forbidden state: $Description")
    }
}

Require-Pattern 'Directory.Build.props' '<FModelRecreateVersion>\d+\.\d+\.\d+</FModelRecreateVersion>' 'the independent semantic version manifest'
Require-Pattern 'Directory.Build.targets' '<Product>FModel-Recreate</Product>' 'FModel-Recreate product metadata'
Require-Pattern 'Directory.Build.targets' 'https://github\.com/apexlions16/FModel-Recreate' 'the independent repository URL'

Require-Pattern 'FModel/App.xaml' 'ShutdownMode="OnExplicitShutdown"' 'explicit startup lifecycle ownership'
Require-Pattern 'FModel/App.xaml' '#7657D5' 'the FModel-Recreate accent identity'
Forbid-Pattern 'FModel/App.xaml' 'StartupUri\s*=' 'StartupUri, which conflicts with the first-run wizard lifecycle'

Require-Pattern 'FModel/App.xaml.cs' 'SettingsStorage\.Load\(' 'FModel-Recreate settings loading'
Require-Pattern 'FModel/App.xaml.cs' 'LocalizationManager\.Initialize\(' 'runtime localization initialization'
Require-Pattern 'FModel/App.xaml.cs' 'new FirstRunWizard\(\)' 'the first-run language wizard'
Require-Pattern 'FModel/App.xaml.cs' '--verify-runtime' 'the packaged executable runtime verification mode'
Require-Pattern 'FModel/App.xaml.cs' 'ShutdownMode = ShutdownMode\.OnMainWindowClose' 'main-window shutdown ownership after startup'

Require-Pattern 'FModel/AppPaths.cs' 'FModel-Recreate' 'the independent application data directory'
Require-Pattern 'FModel/Constants.cs' 'APP_NAME = "FModel-Recreate"' 'the application identity'
Require-Pattern 'FModel/Constants.cs' 'REPOSITORY_URL = "https://github\.com/apexlions16/FModel-Recreate"' 'the fork update and support channel'
Require-Pattern 'FModel/Constants.cs' 'UPSTREAM_REPOSITORY_URL = "https://github\.com/4sval/FModel"' 'upstream attribution'

Require-Pattern 'FModel/Settings/SettingsStorage.cs' 'Normalize\(UserSettings settings\)' 'saved-setting normalization'
Require-Pattern 'FModel/Settings/SettingsStorage.cs' 'Directory\.Exists\(directory\)' 'invalid game directory recovery'
Require-Pattern 'FModel/Localization/LocalizationManager.cs' 'class LocalizationManager' 'the bilingual runtime localization manager'
Require-Pattern 'FModel/Localization/LocalizationCatalog.cs' 'Turkish' 'the Turkish translation catalog'
Require-Pattern 'FModel/Views/FirstRunWizard.cs' 'class FirstRunWizard' 'the first-run language selection experience'

Require-Pattern 'FModel/Services/GitHubUpdateService.cs' 'Constants\.GH_RELEASES' 'updates from FModel-Recreate releases'
Require-Pattern 'FModel/ViewModels/ApiEndpointViewModel.cs' 'DefaultImGuiSettings' 'the local ImGui settings fallback'
Require-Pattern 'FModel/ViewModels/ApiEndpointViewModel.cs' 'FModel-Recreate/' 'the independent API user agent'

Require-Pattern '.github/workflows/qa.yml' '--self-contained true' 'self-contained QA publishing'
Require-Pattern '.github/workflows/qa.yml' 'Verify published runtime' 'QA execution of the packaged executable'
Require-Pattern '.github/workflows/release.yml' '--self-contained true' 'self-contained stable publishing'
Require-Pattern '.github/workflows/release.yml' 'Verify published runtime' 'stable release execution of the packaged executable'
Require-Pattern '.github/workflows/release.yml' 'softprops/action-gh-release' 'GitHub Release publication'

if ($failures.Count -gt 0) {
    $message = "FModel-Recreate invariant guard failed:`n- " + ($failures -join "`n- ")
    throw $message
}

Write-Host 'FModel-Recreate invariant guard passed.'
