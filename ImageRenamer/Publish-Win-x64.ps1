$ErrorActionPreference = 'Stop'

$projectFile = Join-Path $PSScriptRoot 'ImageRenamer.csproj'
$publishFolder = Join-Path $PSScriptRoot 'publish'

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project file not found: $projectFile"
}

# Remove old output first so stale DLLs or language folders cannot remain.
if (Test-Path -LiteralPath $publishFolder) {
    Remove-Item -LiteralPath $publishFolder -Recurse -Force
}

& dotnet publish $projectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:SatelliteResourceLanguages=en `
    --output $publishFolder

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$expectedNames = @('ImageRenamer.exe', 'appsettings.json')
$actualItems = @(Get-ChildItem -LiteralPath $publishFolder -Force)
$unexpectedItems = @($actualItems | Where-Object { $_.Name -notin $expectedNames })
$missingItems = @($expectedNames | Where-Object { -not (Test-Path -LiteralPath (Join-Path $publishFolder $_)) })

if ($unexpectedItems.Count -gt 0 -or $missingItems.Count -gt 0) {
    Write-Warning 'Publish output differs from requested two-file layout.'
    if ($unexpectedItems.Count -gt 0) {
        Write-Warning ('Unexpected items: ' + (($unexpectedItems | ForEach-Object Name) -join ', '))
    }
    if ($missingItems.Count -gt 0) {
        Write-Warning ('Missing items: ' + ($missingItems -join ', '))
    }
    throw 'Publish output validation failed. Review the publish log above.'
}

Write-Host ''
Write-Host 'Publish successful. Output contains only:' -ForegroundColor Green
Write-Host (Join-Path $publishFolder 'ImageRenamer.exe')
Write-Host (Join-Path $publishFolder 'appsettings.json')
