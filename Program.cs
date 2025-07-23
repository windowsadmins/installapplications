using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InstallApplications
{
    public record PackageItem(
        string displayname,
        string file,
        string hash,
        string url,
        string[]? arguments,
        string type,
        string? condition = null
    );

    public record BootstrapConfig(
        PackageItem[] setupassistant,
        PackageItem[] userland
    );

    class Program
    {
        private static readonly HttpClient httpClient = new();
        private static ILogger<Program>? logger;
        private static IConfiguration? configuration;

        static async Task<int> Main(string[] args)
        {
            // Basic setup
            var host = CreateHostBuilder(args).Build();
            logger = host.Services.GetRequiredService<ILogger<Program>>();
            configuration = host.Services.GetRequiredService<IConfiguration>();
            
            logger.LogInformation("Cimian InstallApplications starting for Windows bootstrap...");
            
            try
            {
                // Use Cimian's Azure CDN for bootstrap configuration
                var bootstrapUrl = configuration["BootstrapJsonUrl"] ?? 
                    "https://cimian.ecuad.ca/packages/InstallApplications/bootstrap.json";
                
                logger.LogInformation("Downloading Cimian bootstrap configuration from: {Url}", bootstrapUrl);
                var jsonContent = await httpClient.GetStringAsync(bootstrapUrl);
                var config = JsonSerializer.Deserialize<BootstrapConfig>(jsonContent);
                
                if (config == null)
                {
                    logger.LogError("Failed to parse Cimian bootstrap configuration");
                    return 1;
                }

                // Check if we're in OOBE/Setup Assistant context
                var isSetupAssistant = IsInSetupAssistantContext();
                logger.LogInformation("Windows OOBE/Setup context detected: {IsSetupAssistant}", isSetupAssistant);

                if (isSetupAssistant && config.setupassistant?.Length > 0)
                {
                    logger.LogInformation("Processing Cimian Setup Assistant packages...");
                    await ProcessPackages(config.setupassistant);
                }

                // Check if user is logged in for userland packages
                var isUserLoggedIn = IsUserLoggedIn();
                logger.LogInformation("User session available: {IsUserLoggedIn}", isUserLoggedIn);

                if (isUserLoggedIn && config.userland?.Length > 0)
                {
                    logger.LogInformation("Processing Cimian Userland packages...");
                    await ProcessPackages(config.userland);
                }

                logger.LogInformation("Cimian InstallApplications completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Cimian InstallApplications failed: {Message}", ex.Message);
                return 1;
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddEventLog(settings =>
                    {
                        settings.SourceName = "Cimian InstallApplications";
                    });
                });

        static async Task ProcessPackages(PackageItem[] packages)
        {
            foreach (var package in packages)
            {
                try
                {
                    // Check condition if specified
                    if (!string.IsNullOrEmpty(package.condition) && !EvaluateCondition(package.condition))
                    {
                        logger?.LogInformation("Skipping {DisplayName} - condition not met: {Condition}", 
                            package.displayname, package.condition);
                        continue;
                    }

                    logger?.LogInformation("Processing Cimian package: {DisplayName}", package.displayname);
                    
                    // Download package
                    var tempPath = Path.Combine(Path.GetTempPath(), package.file);
                    await DownloadFile(package.url, tempPath);
                    
                    // Verify hash if specified
                    if (package.hash == "sha256")
                    {
                        // For now, skip hash verification - would need hash values in config
                        logger?.LogInformation("Hash verification configured for {File} (not yet implemented)", package.file);
                    }
                    
                    // Install package
                    await InstallPackage(tempPath, package.arguments);
                    
                    // Cleanup
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                    
                    logger?.LogInformation("Successfully installed Cimian package: {DisplayName}", package.displayname);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to install Cimian package {DisplayName}: {Message}", 
                        package.displayname, ex.Message);
                    // Continue with other packages
                }
            }
        }

        static async Task DownloadFile(string url, string filePath)
        {
            logger?.LogInformation("Downloading from Cimian CDN: {Url} to {FilePath}", url, filePath);
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "InstallApplications-Windows/1.0 Cimian-Bootstrap");
            
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            await using var fileStream = File.Create(filePath);
            await response.Content.CopyToAsync(fileStream);
            
            logger?.LogInformation("Downloaded {FileSize} bytes from Cimian CDN", new FileInfo(filePath).Length);
        }

        static async Task InstallPackage(string filePath, string[]? arguments)
        {
            var args = arguments != null ? string.Join(" ", arguments) : "";
            logger?.LogInformation("Installing Cimian package {FilePath} with arguments: {Arguments}", filePath, args);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Verb = "runas" // Request elevation for system packages
            };
            
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start Cimian package installation: {filePath}");
            }
            
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                logger?.LogWarning("Package installation output: {Output}", output);
                logger?.LogError("Package installation error: {Error}", error);
                throw new InvalidOperationException($"Cimian package installation failed with exit code {process.ExitCode}: {error}");
            }
        }

        static bool EvaluateCondition(string condition)
        {
            return condition switch
            {
                "architecture_x64" => RuntimeInformation.OSArchitecture == Architecture.X64,
                "architecture_arm64" => RuntimeInformation.OSArchitecture == Architecture.Arm64,
                _ => true // Unknown conditions default to true
            };
        }

        static bool IsInSetupAssistantContext()
        {
            // Multiple checks for Windows OOBE/Setup Assistant context
            try
            {
                // Check if explorer.exe is running (not typically running during OOBE)
                var explorerProcesses = Process.GetProcessesByName("explorer");
                var explorerRunning = explorerProcesses.Length > 0;
                
                // Check if we're running as SYSTEM (typical during OOBE)
                var isSystem = Environment.UserName.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase);
                
                // Check if this is the first boot (registry key)
                var isFirstBoot = false;
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\State");
                    var setupState = key?.GetValue("ImageState")?.ToString();
                    isFirstBoot = setupState == "IMAGE_STATE_UNDEPLOYABLE" || setupState == "IMAGE_STATE_GENERALIZE_RESEAL_TO_OOBE";
                }
                catch
                {
                    // Ignore registry errors
                }
                
                logger?.LogInformation("Setup context check - Explorer: {ExplorerRunning}, System: {IsSystem}, FirstBoot: {IsFirstBoot}", 
                    explorerRunning, isSystem, isFirstBoot);
                
                return !explorerRunning || isSystem || isFirstBoot;
            }
            catch
            {
                // If we can't determine, assume we're not in setup context
                return false;
            }
        }

        static bool IsUserLoggedIn()
        {
            // Check if we have an interactive user session
            try
            {
                var userName = Environment.UserName;
                var userDomainName = Environment.UserDomainName;
                
                // If we're running as SYSTEM or in a service context, wait for user
                var hasUser = !string.IsNullOrEmpty(userName) && 
                              !userName.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) &&
                              Environment.UserInteractive;
                
                // Also check if explorer is running (good indicator of user session)
                var explorerProcesses = Process.GetProcessesByName("explorer");
                var explorerRunning = explorerProcesses.Length > 0;
                
                return hasUser && explorerRunning;
            }
            catch
            {
                return false;
            }
        }
    }
}
