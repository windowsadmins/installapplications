using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace InstallApplications
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("InstallApplications for Windows v1.0.0");
            Console.WriteLine("MDM-agnostic bootstrapping tool for Windows");
            Console.WriteLine("Copyright © Windows Admins Open Source 2025");
            Console.WriteLine();
            
            // Parse command line arguments
            if (args.Length == 0)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  installapplications.exe --url <manifest-url>");
                Console.WriteLine("  installapplications.exe --help");
                Console.WriteLine("  installapplications.exe --version");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --url <url>     URL to the installapplications.json manifest");
                Console.WriteLine("  --help          Show this help message");
                Console.WriteLine("  --version       Show version information");
                return 0;
            }
            
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--help":
                    case "-h":
                        Console.WriteLine("InstallApplications Help");
                        Console.WriteLine("========================");
                        Console.WriteLine();
                        Console.WriteLine("This tool downloads and processes an installapplications.json manifest file");
                        Console.WriteLine("to automatically install packages during Windows OOBE or setup scenarios.");
                        Console.WriteLine();
                        Console.WriteLine("Usage Examples:");
                        Console.WriteLine("  installapplications.exe --url https://cimian.ecuad.ca/bootstrap/installapplications.json");
                        Console.WriteLine();
                        Console.WriteLine("Features:");
                        Console.WriteLine("  - Supports multiple package types: MSI, EXE, PowerShell, Chocolatey (.nupkg)");
                        Console.WriteLine("  - Handles setupassistant (OOBE) and userland installation phases");
                        Console.WriteLine("  - Admin privilege escalation for elevated packages");
                        Console.WriteLine("  - Architecture-specific conditional installation");
                        return 0;
                        
                    case "--version":
                    case "-v":
                        Console.WriteLine("InstallApplications version 1.0.0");
                        Console.WriteLine("Built for Windows (.NET 8)");
                        return 0;
                        
                    case "--url":
                        if (i + 1 < args.Length)
                        {
                            string manifestUrl = args[i + 1];
                            return await ProcessManifest(manifestUrl);
                        }
                        else
                        {
                            Console.WriteLine("ERROR: --url requires a URL parameter");
                            return 1;
                        }
                }
            }
            
            Console.WriteLine("ERROR: Invalid arguments. Use --help for usage information.");
            return 1;
        }
        
        static async Task<int> ProcessManifest(string manifestUrl)
        {
            try
            {
                Console.WriteLine($"Downloading manifest from: {manifestUrl}");
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "InstallApplications/1.0.0");
                
                string jsonContent = await httpClient.GetStringAsync(manifestUrl);
                Console.WriteLine("Manifest downloaded successfully");
                
                // Parse the JSON manifest
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;
                
                // Process setupassistant packages first
                if (root.TryGetProperty("setupassistant", out var setupAssistant))
                {
                    Console.WriteLine("\nProcessing Setup Assistant packages...");
                    await ProcessPackages(setupAssistant, "setupassistant");
                }
                
                // Process userland packages
                if (root.TryGetProperty("userland", out var userland))
                {
                    Console.WriteLine("\nProcessing Userland packages...");
                    await ProcessPackages(userland, "userland");
                }
                
                Console.WriteLine("\n✅ InstallApplications completed successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing manifest: {ex.Message}");
                return 1;
            }
        }
        
        static async Task ProcessPackages(JsonElement packages, string phase)
        {
            Console.WriteLine($"Phase: {phase}");
            
            foreach (var package in packages.EnumerateArray())
            {
                try
                {
                    // Extract package information
                    string displayName = package.GetProperty("displayname").GetString() ?? "Unknown Package";
                    string file = package.GetProperty("file").GetString() ?? "";
                    string url = package.GetProperty("url").GetString() ?? "";
                    string type = package.GetProperty("type").GetString() ?? "unknown";
                    
                    Console.WriteLine($"  📦 {displayName}");
                    Console.WriteLine($"     Type: {type}, File: {file}");
                    
                    // Check conditions (simple architecture check for demo)
                    if (package.TryGetProperty("condition", out var condition))
                    {
                        string conditionStr = condition.GetString() ?? "";
                        if (conditionStr.Contains("architecture_x64") && !Environment.Is64BitOperatingSystem)
                        {
                            Console.WriteLine($"     ⏭️  Skipping (architecture condition not met)");
                            continue;
                        }
                        if (conditionStr.Contains("architecture_arm64") && Environment.Is64BitOperatingSystem)
                        {
                            Console.WriteLine($"     ⏭️  Skipping (architecture condition not met)");
                            continue;
                        }
                    }
                    
                    // Download and install package
                    await DownloadAndInstallPackage(displayName, url, file, type, package);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"     ❌ Error processing package: {ex.Message}");
                }
            }
        }
        
        static async Task DownloadAndInstallPackage(string displayName, string url, string fileName, string type, JsonElement packageInfo)
        {
            try
            {
                // Create temp download directory
                string tempDir = Path.Combine(Path.GetTempPath(), "InstallApplications");
                Directory.CreateDirectory(tempDir);
                
                string localPath = Path.Combine(tempDir, fileName);
                
                Console.WriteLine($"     ⬇️  Downloading from: {url}");
                
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Download failed: {response.StatusCode}");
                }
                
                await using var fileStream = File.Create(localPath);
                await response.Content.CopyToAsync(fileStream);
                
                Console.WriteLine($"     💾 Downloaded to: {localPath}");
                
                // Install based on type
                await InstallPackage(localPath, type, packageInfo);
                
                Console.WriteLine($"     ✅ {displayName} installed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     ❌ Failed to install {displayName}: {ex.Message}");
            }
        }
        
        static async Task InstallPackage(string filePath, string type, JsonElement packageInfo)
        {
            switch (type.ToLower())
            {
                case "powershell":
                case "ps1":
                    await RunPowerShellScript(filePath, packageInfo);
                    break;
                    
                case "msi":
                    await RunMsiInstaller(filePath, packageInfo);
                    break;
                    
                case "exe":
                    await RunExecutable(filePath, packageInfo);
                    break;
                    
                case "nupkg":
                    await RunChocolateyInstall(filePath, packageInfo);
                    break;
                    
                default:
                    Console.WriteLine($"     ⚠️  Unknown package type: {type}");
                    break;
            }
        }
        
        static async Task RunPowerShellScript(string scriptPath, JsonElement packageInfo)
        {
            var args = GetArguments(packageInfo);
            string arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {string.Join(" ", args)}";
            
            bool elevated = packageInfo.TryGetProperty("elevated", out var elevatedProp) && elevatedProp.GetBoolean();
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                UseShellExecute = elevated,
                RedirectStandardOutput = !elevated,
                RedirectStandardError = !elevated,
                CreateNoWindow = !elevated
            };
            
            if (elevated)
            {
                startInfo.Verb = "runas";
            }
            
            Console.WriteLine($"     🔧 Running PowerShell: {arguments}");
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"PowerShell script failed with exit code: {process.ExitCode}");
                }
            }
        }
        
        static async Task RunMsiInstaller(string msiPath, JsonElement packageInfo)
        {
            var args = GetArguments(packageInfo);
            string arguments = $"/i \"{msiPath}\" {string.Join(" ", args)}";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            };
            
            Console.WriteLine($"     📦 Running MSI installer: {arguments}");
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"MSI installer failed with exit code: {process.ExitCode}");
                }
            }
        }
        
        static async Task RunExecutable(string exePath, JsonElement packageInfo)
        {
            var args = GetArguments(packageInfo);
            string arguments = string.Join(" ", args);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            };
            
            Console.WriteLine($"     🔧 Running executable: {exePath} {arguments}");
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Executable failed with exit code: {process.ExitCode}");
                }
            }
        }
        
        static async Task RunChocolateyInstall(string nupkgPath, JsonElement packageInfo)
        {
            var args = GetArguments(packageInfo);
            
            // First check if chocolatey is installed
            var chocoCheck = new ProcessStartInfo
            {
                FileName = "choco.exe",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            
            try
            {
                using var checkProcess = Process.Start(chocoCheck);
                if (checkProcess != null)
                {
                    await checkProcess.WaitForExitAsync();
                    if (checkProcess.ExitCode != 0)
                    {
                        throw new Exception("Chocolatey not installed");
                    }
                }
            }
            catch
            {
                throw new Exception("Chocolatey is required for .nupkg installation but was not found");
            }
            
            // Install the nupkg
            string arguments = $"install \"{nupkgPath}\" {string.Join(" ", args)}";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "choco.exe",
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            };
            
            Console.WriteLine($"     🍫 Running Chocolatey: {arguments}");
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Chocolatey install failed with exit code: {process.ExitCode}");
                }
            }
        }
        
        static List<string> GetArguments(JsonElement packageInfo)
        {
            var arguments = new List<string>();
            
            if (packageInfo.TryGetProperty("arguments", out var argsProperty) && argsProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var arg in argsProperty.EnumerateArray())
                {
                    if (arg.ValueKind == JsonValueKind.String)
                    {
                        arguments.Add(arg.GetString() ?? "");
                    }
                }
            }
            
            return arguments;
        }
    }
}
