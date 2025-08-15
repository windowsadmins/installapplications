# Test script to sign x64 binary on ARM64 system
param([string]$FilePath = ".\publish\x64\installapplications.exe")

Write-Host "=== X64 Signing Test Script ===" -ForegroundColor Cyan
Write-Host "Target file: $FilePath"

# Check if file exists
if (-not (Test-Path $FilePath)) {
    Write-Host "ERROR: File not found: $FilePath" -ForegroundColor Red
    exit 1
}

# Get certificate
$cert = Get-ChildItem -Path "Cert:\CurrentUser\My\" | Where-Object {
    $_.Subject -like "*EmilyCarrU Intune Windows Enterprise Certificate*"
} | Select-Object -First 1

if (-not $cert) {
    Write-Host "ERROR: Certificate not found" -ForegroundColor Red
    exit 1
}

Write-Host "Found certificate: $($cert.Subject)" -ForegroundColor Green
Write-Host "Thumbprint: $($cert.Thumbprint)"

# Try PowerShell signing method first
Write-Host "`nAttempting PowerShell Set-AuthenticodeSignature..." -ForegroundColor Yellow
try {
    $result = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $cert -TimestampServer "http://timestamp.digicert.com"
    Write-Host "PowerShell signing result: $($result.Status)"
    
    if ($result.Status -eq "Valid") {
        Write-Host "SUCCESS: PowerShell signing worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "PowerShell signing failed: $($result.StatusMessage)" -ForegroundColor Red
    }
} catch {
    Write-Host "PowerShell signing exception: $($_.Exception.Message)" -ForegroundColor Red
}

# If PowerShell signing failed, try signtool with different approaches
Write-Host "`nAttempting signtool methods..." -ForegroundColor Yellow

# Method 1: Basic signtool
Write-Host "Method 1: Basic signtool"
try {
    $output = & signtool sign /sha1 $cert.Thumbprint /t "http://timestamp.digicert.com" /fd SHA256 /v $FilePath 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: Basic signtool worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "Basic signtool failed: $output" -ForegroundColor Red
    }
} catch {
    Write-Host "Basic signtool exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Method 2: Copy to different location and sign
Write-Host "`nMethod 2: Copy to neutral location"
$tempDir = "C:\temp\signing"
$tempFile = "$tempDir\installapplications.exe"

try {
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }
    
    Copy-Item -Path $FilePath -Destination $tempFile -Force
    Write-Host "Copied to: $tempFile"
    
    # Try to fix permissions on temp file
    & takeown /f $tempFile | Out-Null
    & icacls $tempFile /grant "Everyone:(F)" | Out-Null
    
    $output = & signtool sign /sha1 $cert.Thumbprint /t "http://timestamp.digicert.com" /fd SHA256 /v $tempFile 2>&1
    if ($LASTEXITCODE -eq 0) {
        Copy-Item -Path $tempFile -Destination $FilePath -Force
        Write-Host "SUCCESS: Temp location signing worked!" -ForegroundColor Green
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        exit 0
    } else {
        Write-Host "Temp location signing failed: $output" -ForegroundColor Red
    }
} catch {
    Write-Host "Temp location method exception: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
}

# Method 3: Try different signtool parameters
Write-Host "`nMethod 3: Alternative signtool parameters"
try {
    $output = & signtool sign /sm /s "My" /sha1 $cert.Thumbprint /t "http://timestamp.digicert.com" /fd SHA256 $FilePath 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: Alternative signtool parameters worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "Alternative signtool failed: $output" -ForegroundColor Red
    }
} catch {
    Write-Host "Alternative signtool exception: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nAll signing methods failed!" -ForegroundColor Red
exit 1
