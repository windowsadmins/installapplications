# ESP (Setup Assistant) Detection Script for Intune
# This script checks if the Setup Assistant phase is completed
# Use this for your ESP/pre-login InstallApplications package

$paths = @(
    'HKLM:\SOFTWARE\InstallApplications\Status\SetupAssistant',
    'HKLM:\SOFTWARE\WOW6432Node\InstallApplications\Status\SetupAssistant'
)

foreach ($path in $paths) {
    try {
        $status = Get-ItemProperty -Path $path -ErrorAction Stop
        if ($status.Stage -eq 'Completed' -or $status.Stage -eq 'Skipped') {
            Write-Host "Setup Assistant phase completed successfully (Status: $($status.Stage))"
            Write-Host "Run ID: $($status.RunId)"
            Write-Host "Completion Time: $($status.CompletionTime)"
            Write-Host "Architecture: $($status.Architecture)"
            exit 0
        }
        elseif ($status.Stage -eq 'Failed') {
            Write-Host "Setup Assistant phase failed: $($status.LastError)"
            Write-Host "Exit Code: $($status.ExitCode)"
            exit 1
        }
    } 
    catch {
        # Registry key doesn't exist or can't be read, continue to next path
    }
}

# If we get here, neither path had a "Completed" status
Write-Host "Setup Assistant phase not completed"
exit 1
