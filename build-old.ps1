# InstallApplications Build Script
# Builds and optionally signs the InstallApplications executable for deployment

[CmdletBinding()]
param(
    [switch]$Sign,
    [switch]$NoSign,
    [string]$Thumbprint,
    [ValidateSet("x64", "arm64", "both")]
    [string]$Architecture = "both",
    [switch]$Clean,
    [switch]$Test
)

$ErrorActionPreference = "Stop"

# Enterprise Certificate Configuration
$Global:EnterpriseCertCN = 'EmilyCarrU Intune Windows Enterprise Certificate'

Write-Host "=== InstallApplications Build Script ===" -ForegroundColor Magenta
Write-Host "Architecture: $Architecture" -ForegroundColor Yellow
Write-Host "Code Signing: $Sign" -ForegroundColor Yellow
Write-Host "Clean Build: $Clean" -ForegroundColor Yellow
Write-Host ""

# Function to display messages with different log levels
function Write-Log {
    param (
        [string]$Message,
        [ValidateSet("INFO", "SUCCESS", "WARNING", "ERROR")]
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    switch ($Level) {
        "INFO"    { Write-Host "[$timestamp] [INFO] $Message" -ForegroundColor Cyan }
        "SUCCESS" { Write-Host "[$timestamp] [SUCCESS] $Message" -ForegroundColor Green }
        "WARNING" { Write-Host "[$timestamp] [WARNING] $Message" -ForegroundColor Yellow }
        "ERROR"   { Write-Host "[$timestamp] [ERROR] $Message" -ForegroundColor Red }
    }
}

# Function to check if a command exists
function Test-Command {
    param ([string]$Command)
    return $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Function to get signing certificate thumbprint
function Get-SigningCertThumbprint {
    [OutputType([string])]
    param()
    Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.Subject -like "*CN=$Global:EnterpriseCertCN*" -and
            $_.NotAfter -gt (Get-Date) -and
            $_.HasPrivateKey
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1 -ExpandProperty Thumbprint
}

# Function to ensure signtool is available
function Test-SignTool {
    param(
        [string[]]$PreferredArchOrder = @(
            $(if ($Env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' }
              elseif ($Env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' }
              else { 'x86' }),
            'x86', 'x64', 'arm64'
        )
    )
    function Add-ToPath([string]$dir) {
        if (-not [string]::IsNullOrWhiteSpace($dir) -and
            -not ($env:Path -split ';' | Where-Object { $_ -ieq $dir })) {
            $env:Path = "$dir;$env:Path"
        }
    }
    if (Get-Command signtool.exe -ErrorAction SilentlyContinue) { return }
    $roots = @(
        "${env:ProgramFiles}\Windows Kits\10\bin",
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    )
    try {
        $kitsRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -EA Stop).KitsRoot10
        if ($kitsRoot) { $roots += (Join-Path $kitsRoot 'bin') }
    } catch { }
    $roots = $roots | Where-Object { Test-Path $_ } | Select-Object -Unique
    foreach ($root in $roots) {
        foreach ($arch in $PreferredArchOrder) {
            $candidate = Get-ChildItem -Path (Join-Path $root "*\$arch\signtool.exe") -EA SilentlyContinue |
                         Sort-Object LastWriteTime -Desc | Select-Object -First 1
            if ($candidate) {
                Add-ToPath $candidate.Directory.FullName
                Write-Log "signtool discovered at $($candidate.FullName)" "SUCCESS"
                return
            }
        }
    }
    Write-Log @"
signtool.exe not found.
Install **any** Windows 10/11 SDK _or_ Visual Studio Build Tools
(ensure the **Windows SDK Signing Tools** workload is included),
then run the build again.
"@ "ERROR"
    exit 1
}

# Function to sign packages
function SignPackage {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string]$Thumbprint = $env:SIGN_THUMB
    )
    if (-not (Test-Path $FilePath)) {
        Write-Log "File not found for signing: $FilePath" "WARNING"
        return $false
    }
    
    $tsaList = @(
        'http://timestamp.digicert.com',
        'http://timestamp.sectigo.com',
        'http://timestamp.entrust.net/TSS/RFC3161sha2TS'
    )
    foreach ($tsa in $tsaList) {
        Write-Log "Signing '$FilePath' using $tsa ..." "INFO"
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        & signtool.exe sign `
            /sha1  $Thumbprint `
            /fd    SHA256 `
            /tr    $tsa `
            /td    SHA256 `
            /v `
            "$FilePath"
        if ($LASTEXITCODE -eq 0) {
            Write-Log  "signtool succeeded with $tsa" "SUCCESS"
            return $true
        }
        Write-Log "signtool failed with $tsa (exit $LASTEXITCODE)" "WARNING"
    }
    Write-Log "signtool failed with all timestamp authorities for '$FilePath' - continuing build without signature" "WARNING"
    return $false
}

Write-Log "=== InstallApplications Build Script ===" "INFO"
Write-Log "Task: $Task" "INFO"
Write-Log "Sign: $Sign" "INFO"

# Auto-detect enterprise certificate if available
$autoDetectedThumbprint = $null
if (-not $Sign -and -not $NoSign -and -not $Thumbprint) {
    try {
        $autoDetectedThumbprint = Get-SigningCertThumbprint
        if ($autoDetectedThumbprint) {
            Write-Log "Auto-detected enterprise certificate $autoDetectedThumbprint - will sign binaries for security." "INFO"
            $Sign = $true
            $Thumbprint = $autoDetectedThumbprint
        } else {
            Write-Log "No enterprise certificate found - binaries will be unsigned (may be blocked by Defender)." "WARNING"
        }
    }
    catch {
        Write-Log "Could not check for enterprise certificates: $_" "WARNING"
    }
}

if ($NoSign) {
    Write-Log "NoSign parameter specified - skipping all signing." "INFO"
    $Sign = $false
}

if ($Sign) {
    Test-SignTool
    if (-not $Thumbprint) {
        $Thumbprint = Get-SigningCertThumbprint
        if (-not $Thumbprint) {
            Write-Log "No valid '$Global:EnterpriseCertCN' certificate with a private key found - aborting." "ERROR"
            exit 1
        }
        Write-Log "Auto-selected signing cert $Thumbprint" "INFO"
    } else {
        Write-Log "Using signing certificate $Thumbprint" "INFO"
    }
    $env:SIGN_THUMB = $Thumbprint
} else {
    Write-Log "Build will be unsigned." "INFO"
}

# Clean bin and obj directories
Write-Log "Cleaning build directories..." "INFO"
if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }
if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force }

# Ensure .NET 8 is available
Write-Log "Verifying .NET 8 installation..." "INFO"
if (-not (Test-Command "dotnet")) {
    Write-Log ".NET is not installed or not in PATH. Exiting..." "ERROR"
    exit 1
}

$dotnetVersion = & dotnet --version
Write-Log ".NET version: $dotnetVersion" "INFO"

# Build for multiple architectures
$archs = @("x64", "arm64")
foreach ($arch in $archs) {
    Write-Log "Building InstallApplications for $arch..." "INFO"
    
    $publishDir = "publish\$arch"
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    
    # Publish self-contained single file
    $publishArgs = @(
        "publish"
        "-c", "Release"
        "-r", "win-$arch"
        "--self-contained"
        "-p:PublishSingleFile=true"
        "-p:PublishTrimmed=true"
        "-p:IncludeNativeLibrariesForSelfExtract=true"
        "-o", $publishDir
        "--verbosity", "minimal"
    )
    
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Build failed for $arch with exit code $LASTEXITCODE" "ERROR"
        exit 1
    }
    
    Write-Log "Build completed successfully for $arch" "SUCCESS"
    
    # Sign the executable if signing is enabled
    if ($Sign) {
        $exePath = Get-ChildItem -Path $publishDir -Filter "*.exe" | Select-Object -First 1
        if ($exePath) {
            Write-Log "Signing InstallApplications.exe for $arch..." "INFO"
            $signResult = SignPackage -FilePath $exePath.FullName
            if ($signResult) {
                Write-Log "Signed $($exePath.FullName) successfully" "SUCCESS"
            } else {
                Write-Log "Failed to sign $($exePath.FullName) - continuing build" "WARNING"
            }
        }
    }
}

if ($Task -eq "build") {
    Write-Log "Build task completed. Binaries available in publish/ directory." "SUCCESS"
    exit 0
}

# Create test infrastructure if requested
if ($Test) {
    Write-Log "Setting up test infrastructure..." "INFO"
    
    $testDir = "C:\inetpub\wwwroot\installapps"
    if (-not (Test-Path $testDir)) {
        New-Item -ItemType Directory -Path $testDir -Force | Out-Null
        Write-Log "Created test directory: $testDir" "SUCCESS"
    }
    
    # Copy test manifest
    if (Test-Path "examples\manifest.json") {
        Copy-Item "examples\manifest.json" $testDir -Force
        Write-Log "Copied test manifest to $testDir" "SUCCESS"
    }
    
    # Test commands
    $testCommands = @(
        "Validate manifest: .\publish\x64\InstallApplications.exe validate --repo http://localhost/installapps",
        "Test installation: .\publish\x64\InstallApplications.exe install --repo http://localhost/installapps --phase setupassistant --dry-run",
        "Check service status: .\publish\x64\InstallApplications.exe service --status"
    )
    
    Write-Log "Test commands available:" "INFO"
    foreach ($cmd in $testCommands) {
        Write-Log "  $cmd" "INFO"
    }
}

Write-Log "Build process completed successfully." "SUCCESS"
Write-Log "Binaries available:" "INFO"
Get-ChildItem -Path "publish" -Recurse -Filter "*.exe" | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Log "  $($_.FullName) ($size MB)" "INFO"
}
