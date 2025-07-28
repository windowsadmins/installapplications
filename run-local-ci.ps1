#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local CI/CD Pipeline Runner
    
.DESCRIPTION
    Runs the complete CI/CD pipeline locally to validate changes before pushing to GitHub.
    This script mimics the GitHub Actions workflow to catch issues early.
    
.PARAMETER Stage
    Which stage(s) to run. Default is 'All'
    
.PARAMETER Architecture
    Target architecture(s) to build. Default is 'x64'
    
.PARAMETER Configuration
    Build configuration. Default is 'Release'
    
.PARAMETER SkipTests
    Skip running tests
    
.PARAMETER SkipSigning
    Skip code signing (useful for local development)
    
.PARAMETER Clean
    Perform a clean build
    
.EXAMPLE
    .\run-local-ci.ps1
    Runs the complete pipeline
    
.EXAMPLE
    .\run-local-ci.ps1 -Stage "Build,Test" -Architecture "x64"
    Runs only build and test stages for x64
#>

[CmdletBinding()]
param(
    [ValidateSet("All", "Validate", "Build", "Test", "Package", "Integration")]
    [string[]]$Stage = @("All"),
    
    [ValidateSet("x64", "arm64", "both")]
    [string]$Architecture = "x64",
    
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [switch]$SkipTests,
    [switch]$SkipSigning,
    [switch]$Clean,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Header($message) {
    Write-Host "`n=== $message ===" -ForegroundColor Magenta
}

function Write-Step($message) {
    Write-Host "→ $message" -ForegroundColor Cyan
}

function Write-Success($message) {
    Write-Host "✓ $message" -ForegroundColor Green
}

function Write-Warning($message) {
    Write-Host "⚠ $message" -ForegroundColor Yellow
}

function Write-Error($message) {
    Write-Host "✗ $message" -ForegroundColor Red
}

# Configuration
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$rootPath = $PSScriptRoot
$solutionFile = "InstallApplications.sln"
$projectFile = "InstallApplications.csproj"

Write-Header "Local CI/CD Pipeline"
Write-Host "Stage(s): $($Stage -join ', ')"
Write-Host "Architecture: $Architecture"
Write-Host "Configuration: $Configuration"
Write-Host "Root Path: $rootPath"
Write-Host ""

# Validation Stage
function Invoke-ValidationStage {
    Write-Header "Stage 1: Validation"
    
    Write-Step "Checking prerequisites..."
    
    # Check .NET SDK
    if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        throw ".NET SDK not found. Please install .NET 8.0 SDK"
    }
    
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK version: $dotnetVersion"
    
    # Check solution file
    if (-not (Test-Path $solutionFile)) {
        throw "Solution file not found: $solutionFile"
    }
    Write-Success "Solution file found"
    
    # Check for required files
    $requiredFiles = @("InstallApplications.csproj", "Program.cs")
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path $file)) {
            throw "Required file not found: $file"
        }
    }
    Write-Success "All required files present"
    
    Write-Step "Restoring NuGet packages..."
    dotnet restore $solutionFile --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
    Write-Success "NuGet packages restored"
    
    Write-Step "Checking code formatting..."
    dotnet format --verify-no-changes --verbosity minimal
    if ($LASTEXITCODE -ne 0) { 
        Write-Warning "Code formatting issues detected. Run 'dotnet format' to fix."
    } else {
        Write-Success "Code formatting is correct"
    }
}

# Build Stage
function Invoke-BuildStage {
    Write-Header "Stage 2: Build"
    
    if ($Clean) {
        Write-Step "Cleaning previous builds..."
        dotnet clean $solutionFile --configuration $Configuration --verbosity minimal
        Write-Success "Clean completed"
    }
    
    $architectures = switch ($Architecture) {
        "both" { @("x64", "arm64") }
        default { @($Architecture) }
    }
    
    foreach ($arch in $architectures) {
        Write-Step "Building for win-$arch ($Configuration)..."
        
        dotnet build $solutionFile `
            --configuration $Configuration `
            --runtime "win-$arch" `
            --verbosity minimal `
            --no-restore
        
        if ($LASTEXITCODE -ne 0) { 
            throw "Build failed for win-$arch"
        }
        Write-Success "Build completed for win-$arch"
    }
}

# Test Stage
function Invoke-TestStage {
    Write-Header "Stage 3: Testing"
    
    if ($SkipTests) {
        Write-Warning "Tests skipped by user request"
        return
    }
    
    Write-Step "Running unit tests..."
    
    # Create TestResults directory
    if (-not (Test-Path "TestResults")) {
        New-Item -ItemType Directory -Path "TestResults" -Force | Out-Null
    }
    
    dotnet test $solutionFile `
        --configuration $Configuration `
        --no-build `
        --verbosity normal `
        --logger "trx;LogFileName=test-results.trx" `
        --collect:"XPlat Code Coverage" `
        --results-directory "TestResults"
    
    if ($LASTEXITCODE -ne 0) {
        throw "Unit tests failed"
    }
    Write-Success "All unit tests passed"
    
    # Check for coverage files
    $coverageFiles = Get-ChildItem -Path "TestResults" -Filter "coverage.cobertura.xml" -Recurse
    if ($coverageFiles.Count -gt 0) {
        Write-Success "Code coverage reports generated"
    } else {
        Write-Warning "No code coverage reports found"
    }
}

# Package Stage
function Invoke-PackageStage {
    Write-Header "Stage 4: Packaging"
    
    $architectures = switch ($Architecture) {
        "both" { @("x64", "arm64") }
        default { @($Architecture) }
    }
    
    foreach ($arch in $architectures) {
        Write-Step "Creating package for win-$arch..."
        
        $outputPath = "publish/win-$arch"
        
        dotnet publish $projectFile `
            --configuration $Configuration `
            --runtime "win-$arch" `
            --self-contained true `
            --output $outputPath `
            -p:PublishSingleFile=true `
            -p:PublishTrimmed=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            --verbosity minimal
        
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed for win-$arch"
        }
        
        # Create build info
        $buildInfo = @{
            Version = "dev-local"
            Architecture = "win-$arch"
            Configuration = $Configuration
            BuildDate = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssZ')
            BuildMachine = $env:COMPUTERNAME
        }
        
        $buildInfo | ConvertTo-Json -Indent 2 | Out-File -FilePath "$outputPath/build-info.json" -Encoding UTF8
        
        Write-Success "Package created: $outputPath"
        
        # Create ZIP archive
        $archiveName = "InstallApplications-win-$arch.zip"
        if (Test-Path $archiveName) {
            Remove-Item $archiveName -Force
        }
        
        Compress-Archive -Path "$outputPath/*" -DestinationPath $archiveName
        Write-Success "Archive created: $archiveName"
    }
}

# Integration Stage
function Invoke-IntegrationStage {
    Write-Header "Stage 5: Integration Tests"
    
    $architectures = switch ($Architecture) {
        "both" { @("x64", "arm64") }
        default { @($Architecture) }
    }
    
    foreach ($arch in $architectures) {
        $exePath = "publish/win-$arch/InstallApplications.exe"
        
        if (-not (Test-Path $exePath)) {
            Write-Warning "Executable not found for win-$arch, skipping integration tests"
            continue
        }
        
        Write-Step "Running integration tests for win-$arch..."
        
        # Test 1: Help command
        Write-Host "  Testing --help command..."
        $helpResult = & $exePath --help
        if ($LASTEXITCODE -ne 0) {
            throw "Help command failed for win-$arch"
        }
        
        # Test 2: Version command
        Write-Host "  Testing --version command..."
        $versionResult = & $exePath --version
        if ($LASTEXITCODE -ne 0) {
            throw "Version command failed for win-$arch"
        }
        
        # Test 3: Invalid argument
        Write-Host "  Testing error handling..."
        $errorResult = & $exePath --invalid-argument 2>&1
        if ($LASTEXITCODE -eq 0) {
            throw "Application should have failed with invalid argument for win-$arch"
        }
        
        Write-Success "Integration tests passed for win-$arch"
    }
}

# Main execution
try {
    Push-Location $rootPath
    
    $stages = if ($Stage -contains "All") {
        @("Validate", "Build", "Test", "Package", "Integration")
    } else {
        $Stage
    }
    
    foreach ($stageName in $stages) {
        switch ($stageName) {
            "Validate" { Invoke-ValidationStage }
            "Build" { Invoke-BuildStage }
            "Test" { Invoke-TestStage }
            "Package" { Invoke-PackageStage }
            "Integration" { Invoke-IntegrationStage }
        }
    }
    
    $stopwatch.Stop()
    Write-Header "Pipeline Completed Successfully"
    Write-Success "Total time: $($stopwatch.Elapsed.ToString('mm\:ss'))"
    Write-Host ""
    Write-Host "Artifacts created:"
    
    $artifacts = Get-ChildItem -Path "publish" -Recurse -Filter "*.exe" 2>$null
    foreach ($artifact in $artifacts) {
        Write-Host "  → $($artifact.FullName)"
    }
    
    $archives = Get-ChildItem -Path "." -Filter "InstallApplications-*.zip" 2>$null
    foreach ($archive in $archives) {
        Write-Host "  → $($archive.Name)"
    }
    
    exit 0

} catch {
    Write-Error "Pipeline failed: $($_.Exception.Message)"
    exit 1
} finally {
    Pop-Location
}
