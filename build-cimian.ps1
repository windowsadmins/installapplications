# Cimian InstallApplications Build Script
# Builds and signs the Cimian Windows bootstrap installer

[CmdletBinding()]
param(
    [switch]$Release = $false,
    [switch]$Sign = $false,
    [switch]$Clean = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== Cimian InstallApplications Build ===" -ForegroundColor Magenta
Write-Host "Configuration: $($Release ? 'Release' : 'Debug')" -ForegroundColor Yellow
Write-Host "Code Signing: $Sign" -ForegroundColor Yellow
Write-Host ""

# Change to the script directory
Push-Location $PSScriptRoot

try {
    # Clean if requested
    if ($Clean) {
        Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
        if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
        if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }
        if (Test-Path "publish-cimian") { Remove-Item "publish-cimian" -Recurse -Force }
    }

    # Set configuration
    $config = $Release ? "Release" : "Debug"
    $outputDir = "$PSScriptRoot\publish-cimian"
    
    # Ensure output directory exists
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    # Build the application
    Write-Host "Building Cimian InstallApplications..." -ForegroundColor Cyan
    
    $buildArgs = @(
        "publish"
        "InstallApplications.csproj"
        "-c", $config
        "-r", "win-x64"
        "--self-contained", "true"
        "-p:PublishSingleFile=true"
        "-p:PublishTrimmed=false"
        "-p:IncludeNativeLibrariesForSelfExtract=true"
        "-o", $outputDir
    )
    
    Write-Host "Running: dotnet $($buildArgs -join ' ')" -ForegroundColor Gray
    & dotnet @buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    # Verify output
    $exePath = "$outputDir\installapplications.exe"
    if (-not (Test-Path $exePath)) {
        throw "Build output not found: $exePath"
    }

    # Code signing if requested
    if ($Sign) {
        Write-Host "🔏 Code signing installapplications.exe..." -ForegroundColor Yellow
        
        # Find the enterprise certificate by thumbprint
        $certThumbprint = "1423F241DFF85AD2C8F31DBD70FB597DAC85BA4B"
        $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\$certThumbprint" -ErrorAction SilentlyContinue
        
        if (-not $cert) {
            # Try to find by subject name if thumbprint lookup fails
            $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\" | Where-Object {
                $_.Subject -like "*Cimian*" -or $_.Subject -like "*Emily Carr*" 
            } | Select-Object -First 1
        }
        
        if ($cert) {
            Write-Host "Found certificate: $($cert.Subject) (Thumbprint: $($cert.Thumbprint))" -ForegroundColor Cyan
            
            & signtool sign /sha1 $($cert.Thumbprint) /t http://timestamp.sectigo.com /fd sha256 "$exePath"
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Code signing successful" -ForegroundColor Green
            } else {
                Write-Host "⚠️  Code signing failed but continuing build" -ForegroundColor Yellow
            }
        } else {
            Write-Host "⚠️  No suitable certificate found for signing" -ForegroundColor Yellow
            Write-Host "    Expected certificate thumbprint: $certThumbprint" -ForegroundColor Gray
        }
    }

    $fileInfo = Get-Item $exePath
    $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
    
    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "Output: $exePath" -ForegroundColor White
    Write-Host "Size: $sizeMB MB" -ForegroundColor White
    Write-Host "Last Modified: $($fileInfo.LastWriteTime)" -ForegroundColor White

    # Check if file is signed
    try {
        $signature = Get-AuthenticodeSignature $exePath
        if ($signature.Status -eq "Valid") {
            Write-Host "Code Signature: Valid - $($signature.SignerCertificate.Subject)" -ForegroundColor Green
        } elseif ($signature.Status -eq "NotSigned") {
            Write-Host "Code Signature: Not signed" -ForegroundColor Yellow
        } else {
            Write-Host "Code Signature: $($signature.Status)" -ForegroundColor Red
        }
    } catch {
        Write-Host "Could not check code signature" -ForegroundColor Yellow
    }

    # Copy configuration files to output
    Write-Host ""
    Write-Host "Copying configuration files..." -ForegroundColor Yellow
    
    $configFiles = @("appsettings.json", "bootstrap.json")
    foreach ($configFile in $configFiles) {
        if (Test-Path $configFile) {
            Copy-Item $configFile $outputDir -Force
            Write-Host "Copied: $configFile" -ForegroundColor Gray
        }
    }

    # Create a simple installer package structure
    Write-Host ""
    Write-Host "Creating installer package structure..." -ForegroundColor Yellow
    
    $packageDir = "$PSScriptRoot\package"
    if (Test-Path $packageDir) {
        Remove-Item $packageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    
    # Copy main executable
    Copy-Item "$outputDir\installapplications.exe" "$packageDir\installapplications.exe" -Force
    
    # Copy configuration
    Copy-Item "$outputDir\appsettings.json" "$packageDir\" -Force -ErrorAction SilentlyContinue
    Copy-Item "$outputDir\bootstrap.json" "$packageDir\" -Force -ErrorAction SilentlyContinue
    
    # Create install script
    $installScript = @'
@echo off
REM Cimian InstallApplications Installation Script
echo Installing Cimian InstallApplications...

REM Create directory if it doesn't exist
if not exist "%ProgramFiles%\Cimian" mkdir "%ProgramFiles%\Cimian"

REM Copy executable to system location
copy "installapplications.exe" "%ProgramFiles%\Cimian\installapplications.exe" /Y
copy "appsettings.json" "%ProgramFiles%\Cimian\appsettings.json" /Y
copy "bootstrap.json" "%ProgramFiles%\Cimian\bootstrap.json" /Y

REM Run the bootstrap installer
"%ProgramFiles%\Cimian\installapplications.exe" --url "https://cimian.ecuad.ca/bootstrap/bootstrap.json"

echo Cimian InstallApplications installation completed.
'@
    
    $installScript | Out-File -FilePath "$packageDir\install.bat" -Encoding ASCII
    
    Write-Host "Package created: $packageDir" -ForegroundColor Green
    Write-Host "Contents:" -ForegroundColor White
    Get-ChildItem $packageDir | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Gray }

    Write-Host ""
    Write-Host "Build Summary:" -ForegroundColor Magenta
    Write-Host "- Executable: $exePath ($sizeMB MB)" -ForegroundColor White
    Write-Host "- Package: $packageDir" -ForegroundColor White
    Write-Host "- Ready for Intune deployment" -ForegroundColor Green

} catch {
    Write-Host "Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}
