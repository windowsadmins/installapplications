# Generic Architecture-Aware Detection Script
# This script automatically detects the architecture and checks the appropriate status
# Can be used for both ESP and Userland phases

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("SetupAssistant", "Userland")]
    [string]$Phase
)

# Detect architecture
$arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64" -or $env:PROCESSOR_ARCHITEW6432 -eq "ARM64") {
    $arch = "arm64"
}

Write-Host "Checking $Phase phase completion for $arch architecture"

$paths = @(
    "HKLM:\SOFTWARE\InstallApplications\Status\$Phase",
    "HKLM:\SOFTWARE\WOW6432Node\InstallApplications\Status\$Phase"
)

foreach ($path in $paths) {
    try {
        $status = Get-ItemProperty -Path $path -ErrorAction Stop
        
        # Check if this status is for the correct architecture
        if ($status.Architecture -and $status.Architecture -ne $arch.ToUpper()) {
            Write-Host "Skipping status for different architecture: $($status.Architecture)"
            continue
        }
        
        if ($status.Stage -eq 'Completed') {
            Write-Host "$Phase phase completed successfully"
            Write-Host "Run ID: $($status.RunId)"
            Write-Host "Completion Time: $($status.CompletionTime)"
            Write-Host "Architecture: $($status.Architecture)"
            Write-Host "Bootstrap URL: $($status.BootstrapUrl)"
            
            # Optional: Check if completion is recent (within last 24 hours)
            if ($status.CompletionTime) {
                try {
                    $completionTime = [DateTime]::Parse($status.CompletionTime)
                    $hoursSinceCompletion = (Get-Date) - $completionTime | Select-Object -ExpandProperty TotalHours
                    Write-Host "Hours since completion: $([math]::Round($hoursSinceCompletion, 2))"
                    
                    if ($hoursSinceCompletion -gt 24) {
                        Write-Host "Warning: Completion timestamp is older than 24 hours"
                    }
                }
                catch {
                    Write-Host "Warning: Could not parse completion time: $($status.CompletionTime)"
                }
            }
            
            exit 0
        }
        elseif ($status.Stage -eq 'Failed') {
            Write-Host "$Phase phase failed: $($status.LastError)"
            Write-Host "Exit Code: $($status.ExitCode)"
            Write-Host "Run ID: $($status.RunId)"
            exit 1
        }
        elseif ($status.Stage -eq 'Skipped') {
            Write-Host "$Phase phase was skipped (no packages to install)"
            Write-Host "Run ID: $($status.RunId)"
            exit 0
        }
        else {
            Write-Host "$Phase phase status: $($status.Stage)"
        }
    } 
    catch {
        # Registry key doesn't exist or can't be read, continue to next path
        Write-Host "Could not read status from $path"
    }
}

# If we get here, no path had a "Completed" status
Write-Host "$Phase phase not completed"
exit 1
