using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InstallApplications.Common.Options;
using InstallApplications.Core.Services;
using InstallApplications.Service;
using System.Diagnostics;
using System.ServiceProcess;

namespace InstallApplications;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            return await Parser.Default.ParseArguments<InstallOptions, ServiceOptions, BootstrapOptions, ValidateOptions>(args)
                .MapResult(
                    (InstallOptions opts) => RunInstallAsync(opts),
                    (ServiceOptions opts) => RunServiceCommand(opts),
                    (BootstrapOptions opts) => RunBootstrapAsync(opts),
                    (ValidateOptions opts) => RunValidateAsync(opts),
                    errs => Task.FromResult(1));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunInstallAsync(InstallOptions options)
    {
        try
        {
            Console.WriteLine("InstallApplications - Direct Installation Mode");
            Console.WriteLine($"Repository: {options.RepositoryUrl}");
            Console.WriteLine($"Phase: {options.Phase}");
            Console.WriteLine($"Dry Run: {options.DryRun}");
            Console.WriteLine();

            var host = CreateHostBuilder(options).Build();
            var packageService = host.Services.GetRequiredService<IPackageService>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting direct package installation");

            var packages = await packageService.GetPackagesAsync(options.RepositoryUrl);
            var filteredPackages = packages
                .Where(p => p.Phase.Equals(options.Phase, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Order)
                .ThenBy(p => p.Name)
                .ToList();

            if (filteredPackages.Count == 0)
            {
                Console.WriteLine($"No packages found for phase '{options.Phase}'");
                return 0;
            }

            Console.WriteLine($"Found {filteredPackages.Count} packages to install:");
            foreach (var package in filteredPackages)
            {
                Console.WriteLine($"  - {package.Name} ({package.Type})");
            }
            Console.WriteLine();

            if (options.DryRun)
            {
                Console.WriteLine("Dry run mode - no packages will be installed");
                return 0;
            }

            var downloadPath = @"C:\ProgramData\InstallApplications\Downloads";
            Directory.CreateDirectory(downloadPath);

            var installedPackages = new HashSet<string>();
            var success = true;

            foreach (var package in filteredPackages)
            {
                Console.WriteLine($"Installing {package.Name}...");

                // Download
                if (!await packageService.DownloadPackageAsync(package, downloadPath))
                {
                    Console.WriteLine($"  Failed to download {package.Name}");
                    if (package.Required && !options.ContinueOnError)
                    {
                        success = false;
                        break;
                    }
                    continue;
                }

                // Install
                if (!await packageService.InstallPackageAsync(package, downloadPath))
                {
                    Console.WriteLine($"  Failed to install {package.Name}");
                    if (package.Required && !options.ContinueOnError)
                    {
                        success = false;
                        break;
                    }
                    continue;
                }

                Console.WriteLine($"  Successfully installed {package.Name}");
                installedPackages.Add(package.Name);
            }

            // Cleanup
            if (Directory.Exists(downloadPath))
            {
                Directory.Delete(downloadPath, true);
            }

            Console.WriteLine();
            Console.WriteLine(success ? "Installation completed successfully" : "Installation completed with errors");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during installation: {ex.Message}");
            return 1;
        }
    }

    static Task<int> RunServiceCommand(ServiceOptions options)
    {
        try
        {
            var serviceName = "InstallApplicationsService";

            if (options.Install)
            {
                return Task.FromResult(InstallService(serviceName));
            }
            else if (options.Uninstall)
            {
                return Task.FromResult(UninstallService(serviceName));
            }
            else if (options.Start)
            {
                return Task.FromResult(StartService(serviceName));
            }
            else if (options.Stop)
            {
                return Task.FromResult(StopService(serviceName));
            }
            else if (options.Status)
            {
                return Task.FromResult(ShowServiceStatus(serviceName));
            }
            else
            {
                // If no specific action, check if we should run as service
                if (Environment.UserInteractive)
                {
                    Console.WriteLine("No service action specified. Use --help for available options.");
                    return Task.FromResult(1);
                }
                else
                {
                    // Running as Windows Service
                    var host = CreateServiceHost();
                    return host.RunAsync().ContinueWith(_ => 0);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service operation failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    static async Task<int> RunBootstrapAsync(BootstrapOptions options)
    {
        try
        {
            Console.WriteLine("InstallApplications - Bootstrap Mode");
            Console.WriteLine($"Repository: {options.RepositoryUrl}");
            Console.WriteLine();

            // Store configuration in registry
            StoreConfiguration(options.RepositoryUrl, options.ConfigFile);

            // Install service
            var serviceName = "InstallApplicationsService";
            if (InstallService(serviceName) != 0)
            {
                Console.WriteLine("Failed to install service");
                return 1;
            }

            if (options.AutoStart)
            {
                // Start service
                if (StartService(serviceName) != 0)
                {
                    Console.WriteLine("Failed to start service");
                    return 1;
                }

                Console.WriteLine("Bootstrap completed successfully. Service is running.");
            }
            else
            {
                Console.WriteLine("Bootstrap completed successfully. Service installed but not started.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bootstrap failed: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> RunValidateAsync(ValidateOptions options)
    {
        try
        {
            Console.WriteLine("InstallApplications - Manifest Validation");
            Console.WriteLine($"Repository: {options.RepositoryUrl}");
            Console.WriteLine();

            var host = CreateHostBuilder(new InstallOptions { RepositoryUrl = options.RepositoryUrl }).Build();
            var packageService = host.Services.GetRequiredService<IPackageService>();

            var packages = await packageService.GetPackagesAsync(options.RepositoryUrl);
            
            Console.WriteLine($"Found {packages.Count} packages in manifest");
            
            var validPackages = 0;
            var invalidPackages = 0;

            foreach (var package in packages)
            {
                Console.Write($"  {package.Name} ({package.Type}): ");
                
                var isValid = true;
                var errors = new List<string>();

                // Basic validation
                if (string.IsNullOrEmpty(package.Name))
                {
                    errors.Add("Missing name");
                    isValid = false;
                }

                if (string.IsNullOrEmpty(package.Type))
                {
                    errors.Add("Missing type");
                    isValid = false;
                }

                if (string.IsNullOrEmpty(package.Url))
                {
                    errors.Add("Missing URL");
                    isValid = false;
                }

                if (options.CheckUrls && !string.IsNullOrEmpty(package.Url))
                {
                    // TODO: Check if URL is accessible
                }

                if (isValid)
                {
                    Console.WriteLine("✓ Valid");
                    validPackages++;
                }
                else
                {
                    Console.WriteLine($"✗ Invalid ({string.Join(", ", errors)})");
                    invalidPackages++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Validation complete: {validPackages} valid, {invalidPackages} invalid");
            return invalidPackages > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }
    }

    static IHostBuilder CreateHostBuilder(InstallOptions options) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHttpClient();
                services.AddSingleton<IPackageService, PackageService>();
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    if (options.Verbose)
                    {
                        builder.SetMinimumLevel(LogLevel.Debug);
                    }
                });
            });

    static IHost CreateServiceHost() =>
        Host.CreateDefaultBuilder()
            .UseWindowsService(options =>
            {
                options.ServiceName = "InstallApplicationsService";
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHttpClient();
                services.AddSingleton<IPackageService, PackageService>();
                services.AddHostedService<InstallApplicationsWorker>();
            })
            .ConfigureLogging(logging =>
            {
                logging.AddEventLog(settings =>
                {
                    settings.SourceName = "InstallApplications";
                });
            })
            .Build();

    static int InstallService(string serviceName)
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            var arguments = "service";

            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"create \"{serviceName}\" binPath= \"\\\"{exePath}\\\" {arguments}\" start= auto",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                Console.WriteLine($"Service '{serviceName}' installed successfully");
                return 0;
            }
            else
            {
                Console.WriteLine($"Failed to install service '{serviceName}'");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error installing service: {ex.Message}");
            return 1;
        }
    }

    static int UninstallService(string serviceName)
    {
        try
        {
            StopService(serviceName);

            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete \"{serviceName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            Console.WriteLine($"Service '{serviceName}' uninstalled");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uninstalling service: {ex.Message}");
            return 1;
        }
    }

    static int StartService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status != ServiceControllerStatus.Running)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            Console.WriteLine($"Service '{serviceName}' started");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting service: {ex.Message}");
            return 1;
        }
    }

    static int StopService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
            Console.WriteLine($"Service '{serviceName}' stopped");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping service: {ex.Message}");
            return 1;
        }
    }

    static int ShowServiceStatus(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            Console.WriteLine($"Service '{serviceName}' status: {service.Status}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service '{serviceName}' not found or error: {ex.Message}");
            return 1;
        }
    }

    static void StoreConfiguration(string repositoryUrl, string? configFile)
    {
        try
        {
            var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\InstallApplications");
            key.SetValue("RepositoryUrl", repositoryUrl);
            if (!string.IsNullOrEmpty(configFile))
            {
                key.SetValue("ConfigFile", configFile);
            }
            key.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to store configuration in registry: {ex.Message}");
        }
    }
}
