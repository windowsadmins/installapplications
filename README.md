# InstallApplications for Windows

[![CI/CD Pipeline](https://github.com/your-org/installapplications/actions/workflows/build.yml/badge.svg)](https://github.com/your-org/installapplications/actions/workflows/build.yml)
[![Security Scan](https://github.com/your-org/installapplications/actions/workflows/security.yml/badge.svg)](https://github.com/your-org/installapplications/actions/workflows/security.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![CodeQL](https://github.com/your-org/installapplications/actions/workflows/codeql.yml/badge.svg)](https://github.com/your-org/installapplications/actions/workflows/codeql.yml)

MDM-agnostic software deployment tool for Windows systems. Inspired by the macOS [InstallApplications](https://github.com/macadmins/installapplications) project, this C# implementation provides automated software deployment during Windows OOBE (Out-of-Box Experience) and post-deployment scenarios.

## Overview

InstallApplications for Windows enables IT administrators to:

- **Bootstrap software deployment** during Windows Setup Assistant (OOBE)
- **Orchestrate package installation** from any web-accessible repository
- **Support multiple package formats** (MSI, EXE, PowerShell, Chocolatey, MSIX)
- **Work with any MDM solution** (Intune, JAMF Pro, Workspace ONE, etc.)
- **Provide real-time feedback** to users and administrators
- **Handle dependencies and ordering** automatically

## How It Works

### Windows OOBE/Autopilot Workflow

1. **MDM Trigger**: MDM system deploys InstallApplications via Win32 app or script
2. **Service Installation**: InstallApplications installs itself as a Windows Service
3. **Configuration Download**: Downloads package manifest from configured repository
4. **OOBE Package Installation**: Installs system-level packages during device setup
5. **User Session Packages**: Waits for user login and installs user-specific software
6. **Cleanup and Exit**: Removes itself after successful deployment

### Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   MDM System    │───►│ InstallApps.exe  │───►│ Package Repo    │
│ (Intune, etc.)  │    │ (Windows Service)│    │ (HTTPS/Azure)   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌──────────────────┐
                       │ Package Manifest │
                       │ (JSON/YAML)      │
                       └──────────────────┘
                                │
                                ▼
                       ┌──────────────────┐
                       │ Software Packages│
                       │ MSI/EXE/PS1/MSIX │
                       └──────────────────┘
```

## Quick Start

### 1. Deploy via MDM (Intune Example)

```powershell
# Deploy as Win32 app or PowerShell script
$installCommand = "InstallApplications.exe --repo https://yourrepo.com/packages --bootstrap"
```

### 2. Package Manifest Structure

```json
{
  "packages": [
    {
      "name": "Microsoft Teams",
      "type": "msi",
      "url": "https://repo.com/packages/teams.msi",
      "arguments": "/quiet ALLUSERS=1",
      "phase": "setupassistant",
      "required": true
    },
    {
      "name": "Adobe Reader",
      "type": "exe", 
      "url": "https://repo.com/packages/reader.exe",
      "arguments": "/S",
      "phase": "userland",
      "dependencies": ["Microsoft Teams"]
    }
  ]
}
```

### 3. Supported Package Types

- **MSI**: Windows Installer packages
- **EXE**: Executable installers
- **PowerShell**: `.ps1` scripts with elevation
- **Chocolatey**: `.nupkg` packages
- **MSIX**: Modern Windows packages
- **Registry**: Registry modifications
- **File Copy**: Direct file deployment

## Features

### Core Functionality
- Windows Service architecture
- OOBE/Autopilot integration
- Multiple package format support
- Dependency resolution
- Progress reporting
- Error handling and retry logic
- Cleanup and self-removal

### Planned Features
- GUI progress window
- Advanced logging and telemetry
- Package verification (signatures, hashes)
- Rollback capabilities
- Configuration profiles
- Integration with popular MDM systems

## Installation

### Prerequisites
- Windows 10/11 (1809 or later)
- .NET 8 Runtime
- Administrative privileges

### Command Line Options

```powershell
InstallApplications.exe [OPTIONS]

Options:
  --repo <url>              Package repository URL
  --bootstrap               Install and start service
  --config <path>           Custom configuration file
  --phase <phase>           Run specific phase (setupassistant, userland)
  --dry-run                 Test mode without actual installation
  --verbose                 Enable detailed logging
  --uninstall               Remove service and cleanup
  --help                    Show help information
```

## Configuration

### Repository Structure
```
repository/
├── manifest.json          # Package definitions
├── packages/              # Package files
│   ├── teams.msi
│   ├── reader.exe
│   └── scripts/
│       └── setup.ps1
└── config/                # Configuration files
    └── settings.json
```

### Manifest Schema
```json
{
  "$schema": "https://raw.githubusercontent.com/windowsadmins/installapplications/main/schema.json",
  "version": "1.0",
  "packages": [
    {
      "name": "string",           // Package display name
      "type": "msi|exe|ps1|nupkg|msix|registry|file",
      "url": "string",            // Download URL
      "hash": "string",           // SHA256 hash (optional)
      "arguments": "string",      // Installation arguments
      "phase": "setupassistant|userland",
      "required": "boolean",      // Fail deployment if this fails
      "dependencies": ["string"], // Package dependencies
      "conditions": {             // Installation conditions
        "os_version": ">=10.0.19041",
        "architecture": "x64|arm64",
        "domain_joined": true
      }
    }
  ],
  "settings": {
    "timeout": 3600,             // Package timeout in seconds
    "retries": 3,                // Retry attempts
    "cleanup": true,             // Remove downloaded files
    "reboot_required": false     // Reboot after completion
  }
}
```

## Development

### Building from Source

```powershell
# Clone repository
git clone https://github.com/windowsadmins/installapplications.git
cd installapplications

# Build
dotnet build

# Run tests
dotnet test

# Publish
dotnet publish -c Release -r win-x64 --self-contained
```

### Project Structure

```
src/
├── InstallApplications.Core/     # Core business logic
├── InstallApplications.Service/  # Windows Service
├── InstallApplications.CLI/      # Command line interface
├── InstallApplications.Common/   # Shared utilities
└── InstallApplications.Tests/    # Unit tests
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Original [InstallApplications](https://github.com/macadmins/installapplications) macOS project
- [Swift port](https://github.com/rodchristiansen/installapplications) by Rod Christiansen
- Windows Admin community for feedback and testing

## Support

- 📚 [Documentation](https://github.com/windowsadmins/installapplications/wiki)
- 🐛 [Issue Tracker](https://github.com/windowsadmins/installapplications/issues)
- 💬 [Discussions](https://github.com/windowsadmins/installapplications/discussions)
