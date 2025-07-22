# Build InstallApplications

dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# Create test manifest
mkdir C:\inetpub\wwwroot\installapps
copy examples\manifest.json C:\inetpub\wwwroot\installapps\

# Test direct installation
.\InstallApplications.exe install --repo "http://localhost/installapps" --phase setupassistant --dry-run

# Test bootstrap mode
.\InstallApplications.exe bootstrap --repo "http://localhost/installapps"

# Check service status
.\InstallApplications.exe service --status

# Validate manifest
.\InstallApplications.exe validate --repo "http://localhost/installapps"
