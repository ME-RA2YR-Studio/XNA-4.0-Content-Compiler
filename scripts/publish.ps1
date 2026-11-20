[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$Output = 'dist',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install the .NET 10 SDK first.'
}

$root = Split-Path -Parent $PSScriptRoot
$outputPath = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $root $Output }

Push-Location $root
try {
    if (-not $SkipTests) {
        Write-Host 'Running unit tests...' -ForegroundColor Cyan
        dotnet test 'tests\XnaContentCompiler.Core.Tests\XnaContentCompiler.Core.Tests.csproj' -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw 'Unit tests failed. Publish aborted.'
        }
    }

    Write-Host 'Publishing self-contained single-file executable...' -ForegroundColor Cyan
    dotnet publish 'src\XnaContentCompiler.App\XnaContentCompiler.App.csproj' `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -o $outputPath

    if ($LASTEXITCODE -ne 0) {
        throw 'Publish failed.'
    }

    $exe = Join-Path $outputPath 'XnaContentCompiler.exe'
    if (-not (Test-Path $exe)) {
        throw "Publish finished but '$exe' was not found."
    }

    $file = Get-Item $exe
    $hash = (Get-FileHash $exe -Algorithm SHA256).Hash

    Write-Host ''
    Write-Host 'Publish succeeded.' -ForegroundColor Green
    Write-Host "  File:   $($file.FullName)"
    Write-Host "  Size:   $([math]::Round($file.Length / 1MB, 2)) MB"
    Write-Host "  SHA256: $hash"
}
finally {
    Pop-Location
}
