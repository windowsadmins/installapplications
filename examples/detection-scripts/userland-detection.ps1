# Userland Detection Script for Intune
# This script checks if the Userland phase is completed
# Use this for Win32 apps that depend on userland InstallApplications completion

$paths = @(
    'HKLM:\SOFTWARE\InstallApplications\Status\Userland',
    'HKLM:\SOFTWARE\WOW6432Node\InstallApplications\Status\Userland'
)

foreach ($path in $paths) {
    try {
        $status = Get-ItemProperty -Path $path -ErrorAction Stop
        if ($status.Stage -eq 'Completed' -or $status.Stage -eq 'Skipped') {
            Write-Host "Userland phase completed successfully (Status: $($status.Stage))"
            Write-Host "Run ID: $($status.RunId)"
            Write-Host "Completion Time: $($status.CompletionTime)"
            Write-Host "Architecture: $($status.Architecture)"
            exit 0
        }
        elseif ($status.Stage -eq 'Failed') {
            Write-Host "Userland phase failed: $($status.LastError)"
            Write-Host "Exit Code: $($status.ExitCode)"
            exit 1
        }
    } 
    catch {
        # Registry key doesn't exist or can't be read, continue to next path
    }
}

# If we get here, neither path had a "Completed" status
Write-Host "Userland phase not completed"
exit 1
