# InstallApplications Build Script
# Builds and optionally signs the InstallApplications executable for deployment

            # Verify signature
            Write-Log "Verifying signature..." "INFO"
            $verifyResult = & signtool verify /pa $FilePathdletBinding()]
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
        [ValidateSet("INFO", "WARN", "ERROR", "SUCCESS")]
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    switch ($Level) {
        "INFO"    { Write-Host "[$timestamp] [INFO] $Message" -ForegroundColor White }
        "WARN"    { Write-Host "[$timestamp] [WARN] $Message" -ForegroundColor Yellow }
        "ERROR"   { Write-Host "[$timestamp] [ERROR] $Message" -ForegroundColor Red }
        "SUCCESS" { Write-Host "[$timestamp] [SUCCESS] $Message" -ForegroundColor Green }
    }
}

# Function to check if a command exists
function Test-Command {
    param([string]$Command)
    return [bool](Get-Command $Command -ErrorAction SilentlyContinue)
}

# Function to find signing certificate
function Get-SigningCertificate {
    param([string]$Thumbprint = $null)
    
    if ($Thumbprint) {
        $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction SilentlyContinue
        if ($cert) {
            return $cert
        }
        Write-Log "Certificate with thumbprint $Thumbprint not found" "WARN"
    }
    
    # Search for enterprise certificate by common name
    $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\" | Where-Object {
        $_.Subject -like "*$Global:EnterpriseCertCN*"
    } | Select-Object -First 1
    
    if ($cert) {
        Write-Log "Found enterprise certificate: $($cert.Subject)" "SUCCESS"
        Write-Log "Thumbprint: $($cert.Thumbprint)" "INFO"
        return $cert
    }
    
    Write-Log "No suitable signing certificate found" "WARN"
    return $null
}

# Function to sign executable
function Sign-Executable {
    param(
        [string]$FilePath,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
    )
    
    if (-not (Test-Path $FilePath)) {
        Write-Log "File not found for signing: $FilePath" "ERROR"
        return $false
    }
    
    Write-Log "Signing executable: $([System.IO.Path]::GetFileName($FilePath))" "INFO"
    
    try {
        # Use signtool for signing - requires elevated permissions
        if (-not (Test-Command "signtool")) {
            throw "signtool not found in PATH. Install Windows SDK."
        }
        
        # Check if running as administrator
        $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
        $isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        
        if (-not $isAdmin) {
            Write-Log "Code signing requires administrator privileges. Please run PowerShell as Administrator." "ERROR"
            throw "Access denied: Administrator privileges required for code signing"
        }
        
        $signtoolArgs = @(
            "sign"
            "/sha1", $Certificate.Thumbprint
            "/t", "http://timestamp.digicert.com"
            "/fd", "SHA256"
            "/v"
            $FilePath
        )
        
        Write-Log "Running: signtool $($signtoolArgs -join ' ')" "INFO"
        $result = & signtool @signtoolArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Successfully signed: $([System.IO.Path]::GetFileName($FilePath))" "SUCCESS"
            
            # Verify signature
            Write-Log "Verifying signature..." "INFO"
            $verifyResult = & signtool verify /pa $FilePath
            if ($LASTEXITCODE -eq 0) {
                Write-Log "Signature verification successful" "SUCCESS"
                return $true
            } else {
                Write-Log "Signature verification failed" "ERROR"
                return $false
            }
        } else {
            Write-Log "Signing failed with exit code: $LASTEXITCODE" "ERROR"
            Write-Log "Output: $result" "ERROR"
            return $false
        }
    } catch {
        Write-Log "Error during signing: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Function to build for specific architecture
function Build-Architecture {
    param(
        [string]$Arch,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$SigningCert = $null
    )
    
    Write-Log "Building for $Arch architecture..." "INFO"
    
    $outputDir = "publish\$Arch"
    
    if ($Clean -and (Test-Path $outputDir)) {
        Write-Log "Cleaning output directory: $outputDir" "INFO"
        Remove-Item -Path $outputDir -Recurse -Force
    }
    
    # Ensure output directory exists
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    # Build arguments
    $buildArgs = @(
        "publish"
        "InstallApplications.csproj"
        "--configuration", "Release"
        "--runtime", "win-$Arch"
        "--output", $outputDir
        "--self-contained", "true"
        "--verbosity", "minimal"
    )
    
    try {
        Write-Log "Running: dotnet $($buildArgs -join ' ')" "INFO"
        & dotnet @buildArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code: $LASTEXITCODE"
        }
        
        $executablePath = Join-Path $outputDir "installapplications.exe"
        
        if (-not (Test-Path $executablePath)) {
            throw "Expected executable not found: $executablePath"
        }
        
        # Convert to absolute path for signing
        $executablePath = (Get-Item $executablePath).FullName
        
        $fileInfo = Get-Item $executablePath
        $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
        Write-Log "Build successful: $($fileInfo.Name) ($sizeMB MB)" "SUCCESS"
        
        # Sign the executable if certificate is provided
        if ($SigningCert) {
            if (Sign-Executable -FilePath $executablePath -Certificate $SigningCert) {
                Write-Log "Code signing completed for $Arch" "SUCCESS"
            } else {
                Write-Log "Code signing failed for $Arch" "ERROR"
                return $false
            }
        } else {
            Write-Log "Skipping code signing (no certificate)" "WARN"
        }
        
        return $true
        
    } catch {
        Write-Log "Build failed for $Arch`: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Function to run basic tests
function Test-Build {
    param([string]$ExecutablePath)
    
    Write-Log "Testing build: $ExecutablePath" "INFO"
    
    if (-not (Test-Path $ExecutablePath)) {
        Write-Log "Executable not found for testing: $ExecutablePath" "ERROR"
        return $false
    }
    
    try {
        # Test version output
        Write-Log "Testing --version command..." "INFO"
        $versionOutput = & $ExecutablePath --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Version test passed: $versionOutput" "SUCCESS"
        } else {
            Write-Log "Version test failed with exit code: $LASTEXITCODE" "WARN"
        }
        
        # Test help output  
        Write-Log "Testing --help command..." "INFO"
        $helpOutput = & $ExecutablePath --help 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Help test passed" "SUCCESS"
        } else {
            Write-Log "Help test failed with exit code: $LASTEXITCODE" "WARN"
        }
        
        return $true
        
    } catch {
        Write-Log "Testing failed: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Main build process
try {
    $rootPath = $PSScriptRoot
    Push-Location $rootPath
    
    # Prerequisites check
    Write-Log "Checking prerequisites..." "INFO"
    
    if (-not (Test-Command "dotnet")) {
        throw ".NET CLI not found. Please install .NET 8 SDK."
    }
    
    $dotnetVersion = & dotnet --version
    Write-Log "Using .NET version: $dotnetVersion" "INFO"
    
    # Handle signing certificate
    $signingCert = $null
    if ($Sign -and -not $NoSign) {
        $signingCert = Get-SigningCertificate -Thumbprint $Thumbprint
        if (-not $signingCert) {
            Write-Log "Code signing requested but no certificate available" "ERROR"
            throw "Cannot proceed with signing - certificate not found"
        }
    }
    
    # Build for requested architectures
    $buildResults = @()
    
    $architectures = switch ($Architecture) {
        "x64"  { @("x64") }
        "arm64" { @("arm64") }
        "both" { @("x64", "arm64") }
    }
    
    foreach ($arch in $architectures) {
        Write-Log "" "INFO"
        $success = Build-Architecture -Arch $arch -SigningCert $signingCert
        $buildResults += @{
            Architecture = $arch
            Success = $success
            Path = "publish\$arch\installapplications.exe"
        }
        
        if ($Test -and $success) {
            $execPath = Join-Path $rootPath "publish\$arch\installapplications.exe"
            Test-Build -ExecutablePath $execPath
        }
    }
    
    # Build summary
    Write-Log "" "INFO"
    Write-Log "=== BUILD SUMMARY ===" "INFO"
    
    $successCount = 0
    foreach ($result in $buildResults) {
        if ($result.Success) {
            $successCount++
            $fullPath = Join-Path $rootPath $result.Path
            if (Test-Path $fullPath) {
                $fileInfo = Get-Item $fullPath
                $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
                Write-Log "✅ $($result.Architecture): $($result.Path) ($sizeMB MB)" "SUCCESS"
            } else {
                Write-Log "✅ $($result.Architecture): Built successfully" "SUCCESS"
            }
        } else {
            Write-Log "❌ $($result.Architecture): Build failed" "ERROR"
        }
    }
    
    Write-Log "" "INFO"
    Write-Log "Built $successCount of $($buildResults.Count) architectures successfully" "INFO"
    
    if ($signingCert) {
        Write-Log "All executables signed with certificate: $($signingCert.Subject)" "INFO"
    }
    
    if ($successCount -eq $buildResults.Count) {
        Write-Log "All builds completed successfully!" "SUCCESS"
        exit 0
    } else {
        Write-Log "Some builds failed" "ERROR"
        exit 1
    }
    
} catch {
    Write-Log "Build process failed: $($_.Exception.Message)" "ERROR"
    exit 1
} finally {
    Pop-Location
}
