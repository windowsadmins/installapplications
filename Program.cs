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
        private static string LogDirectory = @"C:\Program Files\InstallApplications\logs";
        private static string LogFile = Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd-HH:mm:ss}.log");

        static void InitializeLogging()
        {
            try
            {
                // Ensure log directory exists
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                // Write initial log entry
                WriteLog("=== InstallApplications Session Started ===");
                WriteLog($"Version: 1.0.0");
                WriteLog($"Process ID: {Environment.ProcessId}");
                WriteLog($"User: {Environment.UserName}");
                WriteLog($"Machine: {Environment.MachineName}");
                WriteLog($"OS: {Environment.OSVersion}");
                WriteLog($"Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
                WriteLog($"Working Directory: {Environment.CurrentDirectory}");
            }
            catch (Exception ex)
            {
                // If logging fails, continue without it
                Console.WriteLine($"Warning: Could not initialize logging: {ex.Message}");
            }
        }

        static void WriteLog(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] {message}";
                
                // Write to console
                Console.WriteLine(logEntry);
                
                // Append to log file
                File.AppendAllText(LogFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // If logging fails, continue silently
            }
        }

        static int Main(string[] args)
        {
            InitializeLogging();
            WriteLog("Main() called with arguments: " + string.Join(" ", args));
            return MainAsync(args).GetAwaiter().GetResult();
        }
        
        static async Task<int> MainAsync(string[] args)
        {
            WriteLog("InstallApplications for Windows v1.0.0");
            WriteLog("MDM-agnostic bootstrapping tool for Windows");
            WriteLog("Copyright © Windows Admins Open Source 2025");
            
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
                        Console.WriteLine("  installapplications.exe --url https://example.com/bootstrap/installapplications.json");
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
                    WriteLog("Processing Userland packages...");
                    Console.WriteLine("\nProcessing Userland packages...");
                    await ProcessPackages(userland, "userland");
                }
                
                // After all packages are installed, start CimianStatus in background mode (SYSTEM context)
                WriteLog("Starting CimianStatus in background mode for pre-login operation...");
                Console.WriteLine("\nStarting CimianStatus in background mode...");
                await StartCimianStatusBackground();

                WriteLog("InstallApplications completed successfully!");
                Console.WriteLine("\n✅ InstallApplications completed successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                WriteLog($"Error processing manifest: {ex.Message}");
                WriteLog($"Stack trace: {ex.StackTrace}");
                Console.WriteLine($"❌ Error processing manifest: {ex.Message}");
                return 1;
            }
        }

        static async Task StartCimianStatusBackground()
        {
            try
            {
                WriteLog("Looking for CimianStatus executable...");
                
                // Check common installation paths for CimianStatus
                string[] possiblePaths = {
                    @"C:\Program Files\Cimian\cimistatus.exe",
                    @"C:\Program Files (x86)\Cimian\cimistatus.exe",
                    @"C:\Tools\Cimian\cimistatus.exe"
                };

                string cimianStatusPath = null;
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        cimianStatusPath = path;
                        WriteLog($"Found CimianStatus at: {path}");
                        break;
                    }
                }

                if (cimianStatusPath == null)
                {
                    WriteLog("CimianStatus executable not found - will be available after user login");
                    return;
                }

                WriteLog("Starting CimianStatus in background service mode...");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = cimianStatusPath,
                    Arguments = "--background",  // Signal to run in SYSTEM context mode
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    WriteLog($"CimianStatus started with PID: {process.Id}");
                    
                    // Don't wait for completion - let it run independently
                    WriteLog("CimianStatus is now running in background mode before user login");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Warning: Could not start CimianStatus in background: {ex.Message}");
                // Don't fail the entire process if CimianStatus startup fails
            }
        }
        
        static async Task ProcessPackages(JsonElement packages, string phase)
        {
            WriteLog($"Processing packages for phase: {phase}");
            Console.WriteLine($"Phase: {phase}");
            
            foreach (var package in packages.EnumerateArray())
            {
                try
                {
                    var displayName = package.GetProperty("name").GetString() ?? "Unknown";
                    var url = package.GetProperty("url").GetString() ?? "";
                    var fileName = package.GetProperty("file").GetString() ?? "";
                    var type = package.GetProperty("type").GetString() ?? "";
                    
                    WriteLog($"Processing package: {displayName} (Type: {type}, File: {fileName})");
                    Console.WriteLine($"  📦 Processing: {displayName}");
                    
                    // Check architecture condition if specified
                    if (package.TryGetProperty("condition", out var condition))
                    {
                        var conditionStr = condition.GetString() ?? "";
                        WriteLog($"Checking condition: {conditionStr}");
                        
                        if (conditionStr.Contains("architecture_x64") && !Environment.Is64BitOperatingSystem)
                        {
                            WriteLog($"Skipping {displayName} - x64 condition not met on this architecture");
                            Console.WriteLine($"     ⏭️  Skipping - x64 condition not met");
                            continue;
                        }
                        if (conditionStr.Contains("architecture_arm64") && Environment.Is64BitOperatingSystem)
                        {
                            WriteLog($"Skipping {displayName} - ARM64 condition not met on this architecture");
                            Console.WriteLine($"     ⏭️  Skipping - ARM64 condition not met");
                            continue;
                        }
                    }
                    
                    await DownloadAndInstallPackage(displayName, url, fileName, type, package);
                    WriteLog($"Successfully completed package: {displayName}");
                }
                catch (Exception ex)
                {
                    WriteLog($"Error processing package: {ex.Message}");
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
                
                WriteLog($"Downloading {displayName} from: {url}");
                Console.WriteLine($"     ⬇️  Downloading from: {url}");
                
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Download failed: {response.StatusCode}");
                }
                
                await using var fileStream = File.Create(localPath);
                await response.Content.CopyToAsync(fileStream);
                
                var fileInfo = new FileInfo(localPath);
                WriteLog($"Downloaded {displayName} to: {localPath} (Size: {fileInfo.Length / 1024 / 1024:F2} MB)");
                Console.WriteLine($"     💾 Downloaded to: {localPath}");
                
                // Install based on type
                WriteLog($"Installing {displayName} using {type} installer...");
                await InstallPackage(localPath, type, packageInfo);
                
                WriteLog($"Successfully installed: {displayName}");
                Console.WriteLine($"     ✅ {displayName} installed successfully");
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to install {displayName}: {ex.Message}");
                Console.WriteLine($"     ❌ Failed to install {displayName}: {ex.Message}");
            }
        }
        
        static async Task InstallPackage(string filePath, string type, JsonElement packageInfo)
        {
            WriteLog($"Installing package: {filePath} (Type: {type})");
            
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
                    WriteLog($"Unknown package type: {type}");
                    Console.WriteLine($"     ⚠️  Unknown package type: {type}");
                    break;
            }
        }
        
        static async Task RunPowerShellScript(string scriptPath, JsonElement packageInfo)
        {
            var args = GetArguments(packageInfo);
            string arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {string.Join(" ", args)}";
            
            bool elevated = packageInfo.TryGetProperty("elevated", out var elevatedProp) && elevatedProp.GetBoolean();
            
            WriteLog($"Running PowerShell script: {scriptPath}");
            WriteLog($"Arguments: {arguments}");
            WriteLog($"Elevated: {elevated}");
            
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
                WriteLog($"PowerShell script completed with exit code: {process.ExitCode}");
                
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
