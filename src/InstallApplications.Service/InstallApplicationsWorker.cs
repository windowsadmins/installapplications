using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InstallApplications.Core.Services;
using InstallApplications.Common.Models;
using System.Diagnostics;

namespace InstallApplications.Service;

public class InstallApplicationsWorker : BackgroundService
{
    private readonly ILogger<InstallApplicationsWorker> _logger;
    private readonly IPackageService _packageService;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly string _repositoryUrl;
    private readonly string _phase;

    public InstallApplicationsWorker(
        ILogger<InstallApplicationsWorker> logger,
        IPackageService packageService,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _packageService = packageService;
        _hostApplicationLifetime = hostApplicationLifetime;

        // Read configuration from registry or config file
        _repositoryUrl = GetRepositoryUrl();
        _phase = GetInstallationPhase();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("InstallApplications service starting...");
            _logger.LogInformation("Repository URL: {RepositoryUrl}", _repositoryUrl);
            _logger.LogInformation("Installation Phase: {Phase}", _phase);

            if (string.IsNullOrEmpty(_repositoryUrl))
            {
                _logger.LogError("Repository URL not configured. Service will exit.");
                _hostApplicationLifetime.StopApplication();
                return;
            }

            // Wait for appropriate context (OOBE vs user session)
            await WaitForAppropriateContext(stoppingToken);

            // Download and process packages
            var packages = await _packageService.GetPackagesAsync(_repositoryUrl);
            var filteredPackages = FilterPackagesForPhase(packages, _phase);

            if (filteredPackages.Count == 0)
            {
                _logger.LogInformation("No packages found for phase '{Phase}'. Service will exit.", _phase);
                _hostApplicationLifetime.StopApplication();
                return;
            }

            // Create download directory
            var downloadPath = @"C:\ProgramData\InstallApplications\Downloads";
            Directory.CreateDirectory(downloadPath);

            // Install packages in dependency order
            var success = await InstallPackagesInOrder(filteredPackages, downloadPath, stoppingToken);

            // Cleanup
            if (success)
            {
                _logger.LogInformation("All packages installed successfully. Cleaning up...");
                await CleanupAsync(downloadPath);
                await UninstallSelfAsync();
            }
            else
            {
                _logger.LogError("Some packages failed to install. Service will exit with error.");
            }

            _hostApplicationLifetime.StopApplication();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in InstallApplications service");
            _hostApplicationLifetime.StopApplication();
        }
    }

    private async Task WaitForAppropriateContext(CancellationToken cancellationToken)
    {
        if (_phase.Equals("setupassistant", StringComparison.OrdinalIgnoreCase))
        {
            // For setup assistant phase, we can start immediately
            _logger.LogInformation("Setup assistant phase - starting immediately");
            return;
        }

        if (_phase.Equals("userland", StringComparison.OrdinalIgnoreCase))
        {
            // For userland phase, wait for user login
            _logger.LogInformation("Userland phase - waiting for user session");
            await WaitForUserSession(cancellationToken);
        }
    }

    private async Task WaitForUserSession(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check if any user is logged in
                var processes = Process.GetProcessesByName("explorer");
                if (processes.Length > 0)
                {
                    _logger.LogInformation("User session detected. Proceeding with userland package installation.");
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking for user session");
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }

    private List<Package> FilterPackagesForPhase(List<Package> packages, string phase)
    {
        return packages
            .Where(p => p.Phase.Equals(phase, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Order)
            .ThenBy(p => p.Name)
            .ToList();
    }

    private async Task<bool> InstallPackagesInOrder(List<Package> packages, string downloadPath, CancellationToken cancellationToken)
    {
        var installedPackages = new HashSet<string>();
        var overallSuccess = true;

        foreach (var package in packages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Check dependencies
                if (!ArePackageDependenciesSatisfied(package, installedPackages))
                {
                    _logger.LogWarning("Dependencies not satisfied for package {PackageName}. Skipping.", package.Name);
                    continue;
                }

                // Check conditions
                if (!await CheckPackageConditions(package))
                {
                    _logger.LogInformation("Conditions not met for package {PackageName}. Skipping.", package.Name);
                    continue;
                }

                _logger.LogInformation("Processing package {PackageName} ({PackageType})", package.Name, package.Type);

                // Download package
                var downloadSuccess = await _packageService.DownloadPackageAsync(package, downloadPath);
                if (!downloadSuccess)
                {
                    _logger.LogError("Failed to download package {PackageName}", package.Name);
                    if (package.Required)
                    {
                        overallSuccess = false;
                        break;
                    }
                    continue;
                }

                // Install package
                var installSuccess = await _packageService.InstallPackageAsync(package, downloadPath);
                if (installSuccess)
                {
                    installedPackages.Add(package.Name);
                    _logger.LogInformation("Successfully installed package {PackageName}", package.Name);
                }
                else
                {
                    _logger.LogError("Failed to install package {PackageName}", package.Name);
                    if (package.Required)
                    {
                        overallSuccess = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing package {PackageName}", package.Name);
                if (package.Required)
                {
                    overallSuccess = false;
                    break;
                }
            }
        }

        return overallSuccess;
    }

    private bool ArePackageDependenciesSatisfied(Package package, HashSet<string> installedPackages)
    {
        return package.Dependencies.All(dep => installedPackages.Contains(dep));
    }

    private async Task<bool> CheckPackageConditions(Package package)
    {
        if (package.Conditions == null)
            return true;

        // Check OS version
        if (!string.IsNullOrEmpty(package.Conditions.OsVersion))
        {
            // TODO: Implement OS version checking
        }

        // Check architecture
        if (!string.IsNullOrEmpty(package.Conditions.Architecture))
        {
            var currentArch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            if (!package.Conditions.Architecture.Equals(currentArch, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check domain join status
        if (package.Conditions.DomainJoined.HasValue)
        {
            // TODO: Implement domain join checking
        }

        // Check file existence
        if (!string.IsNullOrEmpty(package.Conditions.FileExists))
        {
            if (!File.Exists(package.Conditions.FileExists))
            {
                return false;
            }
        }

        return true;
    }

    private async Task CleanupAsync(string downloadPath)
    {
        try
        {
            if (Directory.Exists(downloadPath))
            {
                Directory.Delete(downloadPath, true);
                _logger.LogInformation("Cleaned up download directory: {DownloadPath}", downloadPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup download directory: {DownloadPath}", downloadPath);
        }
    }

    private async Task UninstallSelfAsync()
    {
        try
        {
            _logger.LogInformation("Uninstalling InstallApplications service...");
            
            // Schedule service removal after a delay to allow this process to exit
            var batchScript = @"
timeout /t 5 /nobreak
sc stop InstallApplicationsService
sc delete InstallApplicationsService
del ""%~f0""
";
            
            var tempBatchFile = Path.GetTempFileName() + ".bat";
            await File.WriteAllTextAsync(tempBatchFile, batchScript);
            
            Process.Start(new ProcessStartInfo
            {
                FileName = tempBatchFile,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            
            _logger.LogInformation("Scheduled service removal");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to schedule service removal");
        }
    }

    private string GetRepositoryUrl()
    {
        try
        {
            // Try to read from registry first
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\InstallApplications");
            var repoUrl = key?.GetValue("RepositoryUrl") as string;
            
            if (!string.IsNullOrEmpty(repoUrl))
                return repoUrl;

            // Fall back to environment variable
            return Environment.GetEnvironmentVariable("INSTALLAPPS_REPO_URL") ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read repository URL from registry");
            return string.Empty;
        }
    }

    private string GetInstallationPhase()
    {
        try
        {
            // Check if we're in OOBE/Setup context
            if (IsInOOBEContext())
                return "setupassistant";

            // Otherwise assume userland
            return "userland";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine installation phase");
            return "setupassistant";
        }
    }

    private bool IsInOOBEContext()
    {
        try
        {
            // Check for OOBE registry keys
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\OOBE");
            if (key != null)
            {
                var setupInProgress = key.GetValue("SetupDisplayedEula");
                if (setupInProgress != null)
                    return true;
            }

            // Check for user session
            var processes = Process.GetProcessesByName("explorer");
            var hasUserSession = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return !hasUserSession;
        }
        catch
        {
            return false;
        }
    }
}
