# InstallApplications Deployment Scripts

This directory contains example deployment scripts for various MDM platforms.

## Intune Deployment

Deploy InstallApplications via Intune Win32 app:

```powershell
# Create .intunewin package
IntuneWinAppUtil.exe -c "C:\Source\InstallApplications" -s "InstallApplications.exe" -o "C:\Output"

# Upload to Intune with these settings:
# Install command: InstallApplications.exe bootstrap --repo "https://yourrepo.com/packages"
# Uninstall command: InstallApplications.exe service --uninstall
# Detection rule: File exists - C:\Windows\System32\InstallApplicationsService.exe
```

## Group Policy Deployment

Deploy via GPO startup script:

```batch
@echo off
InstallApplications.exe bootstrap --repo "https://yourrepo.com/packages"
```

## Autopilot Integration

Add to Autopilot profile as required app during OOBE.
