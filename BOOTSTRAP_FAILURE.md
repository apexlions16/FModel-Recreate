# Foundation bootstrap failure

The one-shot bootstrap failed on GitHub Actions. The captured output follows.

``text
  Determining projects to restore...
D:\a\FModel-Recreate\FModel-Recreate\CUE4Parse\CUE4Parse-Conversion\CUE4Parse-Conversion.csproj : warning NU1903: Package 'Microsoft.Bcl.Memory' 9.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-73j8-2gch-69rq [D:\a\FModel-Recreate\FModel-Recreate\FModel\FModel.slnx]
D:\a\FModel-Recreate\FModel-Recreate\CUE4Parse\CUE4Parse\CUE4Parse.csproj : warning NU1903: Package 'Microsoft.Bcl.Memory' 9.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-73j8-2gch-69rq [D:\a\FModel-Recreate\FModel-Recreate\FModel\FModel.slnx]
D:\a\FModel-Recreate\FModel-Recreate\FModel\FModel.csproj : warning NU1903: Package 'Microsoft.Bcl.Memory' 9.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-73j8-2gch-69rq [D:\a\FModel-Recreate\FModel-Recreate\FModel\FModel.slnx]
  Restored D:\a\FModel-Recreate\FModel-Recreate\CUE4Parse\CUE4Parse\CUE4Parse.csproj (in 18.82 sec).
  Restored D:\a\FModel-Recreate\FModel-Recreate\FModel\FModel.csproj (in 18.82 sec).
  Restored D:\a\FModel-Recreate\FModel-Recreate\CUE4Parse\CUE4Parse-Conversion\CUE4Parse-Conversion.csproj (in 18.82 sec).
C:\Program Files\dotnet\sdk\10.0.302\Current\SolutionFile\ImportAfter\Microsoft.NET.Sdk.Solution.targets(27,5): error NETSDK1134: Building a solution with a specific RuntimeIdentifier is not supported. If you would like to publish for a single RID, specify the RID at the individual project level instead. [D:\a\FModel-Recreate\FModel-Recreate\FModel\FModel.slnx]

Build FAILED.

C:\Program Files\dotnet\sdk\10.0.302\Current\SolutionFile\ImportAfter\Microsoft.NET.Sdk.Solution.targets(27,5): error NETSDK1134: Building a solution with a specific RuntimeIdentifier is not supported. If you would like to publish for a single RID, specify the RID at the individual project level instead. [D:\a\FModel-Recreate\FModel-Recreate\FModel\FModel.slnx]
    0 Warning(s)
    1 Error(s)

Time Elapsed 00:00:00.42
Test-Path: D:\a\FModel-Recreate\FModel-Recreate\tools\bootstrap-foundation.ps1:1038
Line |
1038 |  if (Test-Path 'tools' -PathType Container -and -not (Get-ChildItem 't …
     |                                            ~~~~
     | A parameter cannot be found that matches parameter name 'and'.



``
