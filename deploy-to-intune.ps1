# InstallApplications Intune Deployment Script
# Builds, signs, packages, and deploys InstallApplications to Intune
# Updated to use the IntuneWin32App PowerShell module for reliable deployment

[CmdletBinding()]
param(
    [switch]$BuildOnly,
    [switch]$PackageOnly,
    [switch]$UploadOnly,
    [switch]$Force,
    [ValidateSet("x64", "arm64", "both")]
    [string]$Architecture = "both",
    [string]$TenantId,
    [string]$ClientId,
    [string]$ClientSecret
)

$ErrorActionPreference = "Stop"

# Configuration
$Global:AppDisplayName = "InstallApplications Bootstrap for Cimian"
$Global:AppDescription = "Installs InstallApplications tool during Windows OOBE to bootstrap Cimian software management system"
$Global:Publisher = "Emily Carr University of Art + Design"
$Global:EnterpriseCertCN = 'EmilyCarrU Intune Windows Enterprise Certificate'

Write-Host "=== InstallApplications Intune Deployment ===" -ForegroundColor Magenta
Write-Host "Architecture: $Architecture" -ForegroundColor Yellow
Write-Host "Build Only: $BuildOnly" -ForegroundColor Yellow
Write-Host "Package Only: $PackageOnly" -ForegroundColor Yellow
Write-Host "Upload Only: $UploadOnly" -ForegroundColor Yellow
Write-Host ""

# Paths
$scriptRoot = $PSScriptRoot
$intuneDir = Join-Path (Split-Path $scriptRoot -Parent) "intune"
$publishDir = Join-Path $scriptRoot "publish"

# Required tools
$intuneWinAppUtilPath = "C:\Tools\IntuneWinAppUtil\IntuneWinAppUtil.exe"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "SUCCESS" { "Green" }
        "WARNING" { "Yellow" }
        default { "White" }
    }
    Write-Host "$timestamp [$Level] $Message" -ForegroundColor $color
}

function Test-Prerequisites {
    Write-Host "🔍 Checking prerequisites..." -ForegroundColor Yellow
    
    # Check .NET CLI
    if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        Write-Log ".NET CLI not found. Please install .NET 8 SDK." -Level "ERROR"
        return $false
    }
    Write-Log "Found .NET CLI: $(dotnet --version)" -Level "SUCCESS"
    
    # Check IntuneWinAppUtil
    if (-not (Test-Path $intuneWinAppUtilPath)) {
        Write-Log "IntuneWinAppUtil.exe not found at $intuneWinAppUtilPath" -Level "ERROR"
        Write-Log "Please download Microsoft Win32 Content Prep Tool from: https://github.com/Microsoft/Microsoft-Win32-Content-Prep-Tool" -Level "ERROR"
        return $false
    }
    Write-Log "Found IntuneWinAppUtil: $intuneWinAppUtilPath" -Level "SUCCESS"
    
    # Check signing certificate
    $cert = Get-ChildItem -Path "Cert:\CurrentUser\My\" -ErrorAction SilentlyContinue | Where-Object {
        $_.Subject -like "*$Global:EnterpriseCertCN*"
    } | Select-Object -First 1
    
    if (-not $cert) {
        Write-Log "Enterprise certificate not found: $Global:EnterpriseCertCN" -Level "ERROR"
        return $false
    }
    Write-Log "Found enterprise certificate: $($cert.Subject)" -Level "SUCCESS"
    
    return $true
}

function Build-InstallApplications {
    param([string[]]$Architectures)
    
    Write-Host "🔨 Building InstallApplications..." -ForegroundColor Yellow
    
    try {
        Push-Location $scriptRoot
        
        foreach ($arch in $Architectures) {
            Write-Log "Building for $arch architecture..." -Level "INFO"
            
            $buildParams = @{
                Architecture = $arch
                Sign = $true
                Clean = $true
            }
            
            & ".\build.ps1" @buildParams
            
            if ($LASTEXITCODE -ne 0) {
                throw "Build failed for $arch architecture"
            }
            
            $execPath = Join-Path $publishDir "$arch\installapplications.exe"
            if (-not (Test-Path $execPath)) {
                throw "Expected executable not found: $execPath"
            }
            
            Write-Log "Successfully built and signed: $arch" -Level "SUCCESS"
        }
        
        return $true
        
    } catch {
        Write-Log "Build failed: $($_.Exception.Message)" -Level "ERROR"
        return $false
    } finally {
        Pop-Location
    }
}

function Copy-FilesToIntune {
    param([string[]]$Architectures)
    
    Write-Host "📁 Copying files to Intune package directories..." -ForegroundColor Yellow
    
    try {
        foreach ($arch in $Architectures) {
            $sourceDir = Join-Path $publishDir $arch
            $targetDir = Join-Path $intuneDir $arch
            
            # Create target directory
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }
            
            # Copy executable
            $sourceExe = Join-Path $sourceDir "installapplications.exe"
            $targetExe = Join-Path $targetDir "installapplications.exe"
            
            if (Test-Path $sourceExe) {
                Copy-Item $sourceExe $targetExe -Force
                Write-Log "Copied executable for $arch" -Level "SUCCESS"
            } else {
                throw "Source executable not found: $sourceExe"
            }
            
            # Copy supporting files
            $supportingFiles = @(
                "appsettings.json",
                "install.ps1",
                "detect.ps1",
                "assignments.json"
            )
            
            foreach ($file in $supportingFiles) {
                $sourcePath = Join-Path $intuneDir $file
                $targetPath = Join-Path $targetDir $file
                
                if (Test-Path $sourcePath) {
                    Copy-Item $sourcePath $targetPath -Force
                    Write-Log "Copied $file for $arch" -Level "SUCCESS"
                }
            }
        }
        
        return $true
        
    } catch {
        Write-Log "Copy failed: $($_.Exception.Message)" -Level "ERROR"
        return $false
    }
}

function Create-IntuneWinPackage {
    param([string]$Architecture)
    
    Write-Host "📦 Creating .intunewin package for $Architecture..." -ForegroundColor Yellow
    
    # Ensure IntuneWin32App module is available
    if (-not (Get-Module -ListAvailable -Name "IntuneWin32App")) {
        Write-Log "Installing IntuneWin32App PowerShell module..." -Level "INFO"
        Install-Module -Name IntuneWin32App -Force -AllowClobber
        Write-Log "IntuneWin32App module installed successfully" -Level "SUCCESS"
    }
    
    Import-Module IntuneWin32App -Force
    
    try {
        $sourceFolder = Join-Path $intuneDir $Architecture
        $setupFile = "install.ps1"
        $outputFolder = Join-Path $intuneDir "packages"
        
        # Create output folder
        if (-not (Test-Path $outputFolder)) {
            New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
        }
        
        # Remove existing .intunewin file
        $existingPackage = Join-Path $outputFolder "install.intunewin"
        if (Test-Path $existingPackage) {
            Remove-Item $existingPackage -Force
            Write-Log "Removed existing package: install.intunewin" -Level "INFO"
        }
        
        # Create .intunewin package using IntuneWin32App module
        Write-Log "Creating .intunewin package using IntuneWin32App module..." -Level "INFO"
        
        $intuneWinFile = New-IntuneWin32AppPackage -SourceFolder $sourceFolder -SetupFile $setupFile -OutputFolder $outputFolder -IntuneWinAppUtilPath $intuneWinAppUtilPath -Verbose
        
        if ($intuneWinFile -and (Test-Path $intuneWinFile.Path)) {
            $fileInfo = Get-Item $intuneWinFile.Path
            $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
            Write-Log "Successfully created package: $($fileInfo.Name) ($sizeMB MB)" -Level "SUCCESS"
            return $intuneWinFile.Path
        } else {
            throw "Package creation failed"
        }
        
    } catch {
        Write-Log "Package creation failed for $Architecture`: $($_.Exception.Message)" -Level "ERROR"
        return $null
    }
}

function Connect-ToIntune {
    param(
        [string]$TenantId,
        [string]$ClientId,
        [string]$ClientSecret
    )
    
    Write-Log "Connecting to Microsoft Graph for Intune..." -Level "INFO"
    
    try {
        # Connect using IntuneWin32App module
        $authParams = @{
            TenantId = $TenantId
            ClientId = $ClientId
            ClientSecret = $ClientSecret
        }
        
        Connect-MSIntuneGraph @authParams
        Write-Log "Connected to Microsoft Graph successfully" -Level "SUCCESS"
        return $true
        
    } catch {
        Write-Log "Failed to connect to Microsoft Graph: $($_.Exception.Message)" -Level "ERROR"
        throw
    }
}

function Deploy-ToIntune {
    param(
        [string]$PackagePath,
        [string]$Architecture
    )
    
    Write-Host "🚀 Deploying $Architecture package to Intune..." -ForegroundColor Yellow
    
    try {
        # Read package info
        $packageInfoPath = Join-Path $intuneDir "package-info.json"
        if (Test-Path $packageInfoPath) {
            $packageInfo = Get-Content $packageInfoPath | ConvertFrom-Json
        } else {
            # Default package info if file doesn't exist
            $packageInfo = @{
                DisplayName = "$Global:AppDisplayName"
                Description = $Global:AppDescription
                Publisher = $Global:Publisher
                InstallCommand = "PowerShell.exe -ExecutionPolicy Bypass -File install.ps1"
                UninstallCommand = "PowerShell.exe -Command `"Remove-Item -Path 'HKLM:\SOFTWARE\InstallApplications' -Recurse -Force -ErrorAction SilentlyContinue`""
            }
        }
        
        # Load assignments
        $assignmentsFile = Join-Path $intuneDir "assignments.json"
        if (Test-Path $assignmentsFile) {
            $assignments = Get-Content $assignmentsFile | ConvertFrom-Json
        } else {
            Write-Log "No assignments.json found, will deploy without assignments" -Level "WARNING"
            $assignments = @()
        }
        
        Write-Log "Creating Win32 application in Intune..." -Level "INFO"
        
        # Create requirement rule
        $requirementRule = New-IntuneWin32AppRequirementRule -Architecture $Architecture -MinimumSupportedWindowsRelease "W11_22H2"
        
        # Create detection rule (registry-based)
        $detectionRule = New-IntuneWin32AppDetectionRuleRegistry -KeyPath "HKEY_LOCAL_MACHINE\SOFTWARE\InstallApplications" -ValueName "BootstrapStatus" -Check "StringEquals" -StringValue "Success" -DetectionType "exists"
        
        # Create the Win32 app
        $appDisplayName = "$($packageInfo.DisplayName) ($Architecture)"
        $win32App = Add-IntuneWin32App -FilePath $PackagePath -DisplayName $appDisplayName -Description $packageInfo.Description -Publisher $packageInfo.Publisher -InstallCommandLine $packageInfo.InstallCommand -UninstallCommandLine $packageInfo.UninstallCommand -InstallExperience "system" -RestartBehavior "suppress" -DetectionRule $detectionRule -RequirementRule $requirementRule -Verbose
        
        if ($win32App) {
            Write-Log "Application uploaded successfully: $($win32App.displayName)" -Level "SUCCESS"
            Write-Log "Application ID: $($win32App.id)" -Level "SUCCESS"
            
            # Apply assignments if available
            if ($assignments.Count -gt 0) {
                Write-Log "Applying device group assignments..." -Level "INFO"
                foreach ($assignment in $assignments) {
                    try {
                        # Create assignment for each group
                        Add-IntuneWin32AppAssignmentGroup -ID $win32App.id -GroupID $assignment.groupId -Intent $assignment.intent -Notification $assignment.notification | Out-Null
                        Write-Log "Assigned to group: $($assignment.groupName)" -Level "SUCCESS"
                    } catch {
                        Write-Log "Failed to assign to group $($assignment.groupName): $($_.Exception.Message)" -Level "ERROR"
                    }
                }
            }
            
            return $win32App.id
        } else {
            throw "Application upload failed"
        }
        
    } catch {
        Write-Log "Intune deployment failed for $Architecture`: $($_.Exception.Message)" -Level "ERROR"
        throw
    }
}

# Main execution
try {
    if (-not (Test-Prerequisites)) {
        throw "Prerequisites check failed"
    }
    
    # Determine architectures to process
    $architectures = switch ($Architecture) {
        "x64"  { @("x64") }
        "arm64" { @("arm64") }
        "both" { @("x64", "arm64") }
    }
    
    # Build phase
    if (-not $PackageOnly -and -not $UploadOnly) {
        if (-not (Build-InstallApplications -Architectures $architectures)) {
            throw "Build phase failed"
        }
        
        if (-not (Copy-FilesToIntune -Architectures $architectures)) {
            throw "Copy phase failed"
        }
    }
    
    # Package phase
    $packages = @()
    if (-not $UploadOnly) {
        foreach ($arch in $architectures) {
            $packagePath = Create-IntuneWinPackage -Architecture $arch
            if ($packagePath) {
                $packages += @{
                    Architecture = $arch
                    Path = $packagePath
                }
            } else {
                throw "Package creation failed for $arch"
            }
        }
    }
    
    # Upload phase
    if (-not $BuildOnly -and -not $PackageOnly) {
        if (-not $TenantId -or -not $ClientId -or -not $ClientSecret) {
            Write-Log "Upload requires TenantId, ClientId, and ClientSecret parameters" -Level "ERROR"
            throw "Missing authentication parameters for upload"
        }
        
        # Connect to Microsoft Graph using IntuneWin32App module
        Connect-ToIntune -TenantId $TenantId -ClientId $ClientId -ClientSecret $ClientSecret
        
        foreach ($package in $packages) {
            $appId = Deploy-ToIntune -PackagePath $package.Path -Architecture $package.Architecture
            if (-not $appId) {
                throw "Deployment failed for $($package.Architecture)"
            }
            Write-Log "Successfully deployed $($package.Architecture) app with ID: $appId" -Level "SUCCESS"
        }
    }
    
    Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
    
} catch {
    Write-Log "Deployment failed: $($_.Exception.Message)" -Level "ERROR"
    exit 1
}
