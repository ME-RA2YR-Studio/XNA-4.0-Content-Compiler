[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$Test,

    [switch]$Run,

    [switch]$Clean,

    [switch]$CleanOnly,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install the .NET 10 SDK first.'
}

$root = Split-Path -Parent $PSScriptRoot
$testProject = 'tests\XnaContentCompiler.Core.Tests\XnaContentCompiler.Core.Tests.csproj'

function Remove-BuildArtifacts {
    $targets = @()

    foreach ($scope in @('src', 'tests')) {
        $scopePath = Join-Path $root $scope
        if (Test-Path -LiteralPath $scopePath) {
            $targets += Get-ChildItem -LiteralPath $scopePath -Recurse -Directory -Force |
                Where-Object { $_.Name -eq 'bin' -or $_.Name -eq 'obj' }
        }
    }

    $targets += Join-Path $root 'dist'
    $targets += Join-Path $root 'TestResults'

    foreach ($target in $targets) {
        if (-not (Test-Path -LiteralPath $target)) {
            continue
        }

        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "Removed $target"
    }
}

Push-Location $root
try {
    if ($Clean -or $CleanOnly) {
        Write-Host 'Cleaning build output...' -ForegroundColor Cyan
        Remove-BuildArtifacts
    }

    if ($CleanOnly) {
        Write-Host 'Clean completed.' -ForegroundColor Green
        return
    }

    Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
    dotnet build 'XnaContentCompiler.slnx' -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed.'
    }

    if ($Test) {
        Write-Host 'Running unit tests...' -ForegroundColor Cyan
        dotnet test $testProject -c $Configuration --no-build
        if ($LASTEXITCODE -ne 0) {
            throw 'Unit tests failed.'
        }
    }

    $exe = Join-Path $root "src\XnaContentCompiler.App\bin\$Configuration\net10.0-windows\XnaContentCompiler.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Build finished but '$exe' was not found."
    }

    Write-Host ''
    Write-Host 'Build succeeded.' -ForegroundColor Green
    Write-Host "  Executable: $exe"

    if ($Run) {
        Write-Host 'Starting XNA Content Compiler...' -ForegroundColor Cyan

        if ($AppArgs.Count -gt 0) {
            & $exe @AppArgs
            exit $LASTEXITCODE
        }

        Start-Process -FilePath $exe | Out-Null
    }
}
finally {
    Pop-Location
}
