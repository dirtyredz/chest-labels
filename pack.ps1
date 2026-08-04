<#
    Builds a release archive laid out the way Nexus and Vortex expect:

        BepInEx/plugins/ChestLabels/ChestLabels.dll

    Deliberately not the dev deploy path (plugins/MoonlightPeaksMods/ChestLabels), which only
    exists to keep hand-built DLLs clear of Vortex during development.
#>

$ErrorActionPreference = 'Stop'

$modRoot  = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$project  = Join-Path $modRoot 'src\ChestLabels.csproj'

# Single source of truth for the version, so the archive can never disagree with the DLL.
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ }
if (-not $version) { throw "Could not read <Version> from $project" }

Write-Host "Packing Chest Labels $version"

# SkipDeploy keeps a release build from overwriting the copy under test in the game folder.
dotnet build $project -c Release -p:SkipDeploy=true
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

Write-Host 'Running tests'
dotnet run --project (Join-Path $modRoot 'tests\ChestLabels.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed - not packing' }

$dll = Join-Path $modRoot 'src\bin\Release\netstandard2.1\ChestLabels.dll'
if (-not (Test-Path $dll)) { throw "Built DLL not found at $dll" }

$staging = Join-Path $env:TEMP "ChestLabels-pack-$([guid]::NewGuid().ToString('N'))"
$target  = Join-Path $staging 'BepInEx\plugins\ChestLabels'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $dll $target

$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$archive = Join-Path $dist "ChestLabels-$version.zip"
if (Test-Path $archive) { Remove-Item $archive }

Compress-Archive -Path (Join-Path $staging 'BepInEx') -DestinationPath $archive
Remove-Item $staging -Recurse -Force

Write-Host "Created $archive"
Write-Host 'Extract it over the game folder to install.'
