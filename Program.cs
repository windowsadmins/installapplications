using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace InstallApplications
{
    class Program
    {
        private static string LogDirectory = @"C:\Program Files\InstallApplications\logs";
        private static string LogFile = Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd-HHmmss}.log");

        static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        static bool TryRestartAsAdministrator(string[] args)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "installapplications.exe"),
                    Arguments = string.Join(" ", args),
                    UseShellExecute = true,
                    Verb = "runas",  // Request elevation
                    CreateNoWindow = false
                };

                WriteLog("InstallApplications requires administrator privileges. Requesting elevation...");
                Console.WriteLine("InstallApplications requires administrator privileges. Requesting elevation...");
                
                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    WriteLog($"Elevated process started with PID: {process.Id}");
                    Console.WriteLine("Elevated process started. This instance will now exit.");
                    return true;
                }
                else
                {
                    WriteLog("Failed to start elevated process - user may have denied elevation");
                    Console.WriteLine("Failed to start elevated process. User may have denied elevation.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Error attempting to restart as administrator: {ex.Message}");
                Console.WriteLine($"Error attempting to restart as administrator: {ex.Message}");
                return false;
            }
        }

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
                WriteLog($"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
                WriteLog($"OS Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
                WriteLog($"Working Directory: {Environment.CurrentDirectory}");
                WriteLog($"Command Line: {Environment.CommandLine}");
                WriteLog($"Is Interactive: {Environment.UserInteractive}");
                WriteLog($"Current User: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}");
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
                
                // Always write to log file first
                File.AppendAllText(LogFile, logEntry + Environment.NewLine);
                
                // Write to console with flush to ensure visibility
                Console.WriteLine(logEntry);
                Console.Out.Flush();
            }
            catch
            {
                // If logging fails, continue silently but try console fallback
                try
                {
                    Console.WriteLine($"[LOG ERROR] {message}");
                }
                catch { }
            }
        }

        static int Main(string[] args)
        {
            InitializeLogging();
            WriteLog("Main() called with arguments: " + string.Join(" ", args));
            
            // Check if running as administrator
            if (!IsRunningAsAdministrator())
            {
                WriteLog("InstallApplications is not running as Administrator");
                Console.WriteLine();
                Console.WriteLine("⚠️  InstallApplications requires administrator privileges");
                Console.WriteLine("    Package installations need elevated access to install to Program Files,");
                Console.WriteLine("    write to HKLM registry, install services, and manage system components.");
                Console.WriteLine();
                
                // Attempt to restart as administrator
                if (TryRestartAsAdministrator(args))
                {
                    WriteLog("Successfully launched elevated process. Exiting current instance.");
                    return 0; // Success - elevated process will handle the work
                }
                else
                {
                    WriteLog("Failed to obtain administrator privileges. Cannot continue.");
                    Console.WriteLine("❌ Failed to obtain administrator privileges. Installation cannot continue.");
                    Console.WriteLine();
                    Console.WriteLine("Please run InstallApplications as Administrator, or use:");
                    Console.WriteLine($"  sudo {Environment.ProcessPath ?? "installapplications.exe"} {string.Join(" ", args)}");
                    return 1; // Error - elevation failed
                }
            }
            
            WriteLog("Running with administrator privileges ✓");
            Console.WriteLine("✅ Running with administrator privileges");
            Console.WriteLine();
            
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
                string displayName = "Unknown Package"; // Default value for error handling
                try
                {
                    displayName = package.GetProperty("name").GetString() ?? "Unknown";
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
                        
                        // Get actual processor architecture - use RuntimeInformation for accurate detection
                        string actualArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
                        WriteLog($"Detected runtime architecture: {actualArchitecture}");
                        
                        // Skip x64 packages on non-x64 systems 
                        // Note: RuntimeInformation reports "X64" for AMD64/Intel 64-bit, "ARM64" for ARM64
                        if (conditionStr.Contains("architecture_x64") && actualArchitecture != "X64")
                        {
                            WriteLog($"Skipping {displayName} - x64 condition not met on {actualArchitecture} architecture");
                            Console.WriteLine($"     ⏭️  Skipping - x64 condition not met on {actualArchitecture}");
                            continue;
                        }
                        
                        // Skip ARM64 packages on non-ARM64 systems
                        if (conditionStr.Contains("architecture_arm64") && actualArchitecture != "ARM64")
                        {
                            WriteLog($"Skipping {displayName} - ARM64 condition not met on {actualArchitecture} architecture");
                            Console.WriteLine($"     ⏭️  Skipping - ARM64 condition not met on {actualArchitecture}");
                            continue;
                        }
                    }
                    
                    await DownloadAndInstallPackage(displayName, url, fileName, type, package);
                    WriteLog($"Successfully completed package: {displayName}");
                    Console.WriteLine($"     ✅ {displayName} installed successfully");
                }
                catch (Exception ex)
                {
                    WriteLog($"Failed to install package {displayName}: {ex.Message}");
                    Console.WriteLine($"     ❌ Failed to install package {displayName}: {ex.Message}");
                    // Continue with next package instead of stopping entire process
                    // Note: We don't re-throw because we want to continue with other packages
                    // DO NOT log "Successfully completed" for failed packages - that's the bug you found!
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
                
                // Ensure the file stream is completely closed before proceeding
                {
                    await using var fileStream = File.Create(localPath);
                    await response.Content.CopyToAsync(fileStream);
                    await fileStream.FlushAsync();
                } // fileStream is disposed here
                
                // Add a small delay to ensure file handle is released
                await Task.Delay(100);
                
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
                // Re-throw the exception so the caller knows the installation failed
                throw;
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
            
            // Since InstallApplications is already running as admin, all PowerShell scripts should inherit admin privileges
            // This ensures chocolatey and other system installers work properly
            bool needsElevation = true; // Always run elevated since we're in an admin context
            
            WriteLog($"Running PowerShell script: {scriptPath}");
            WriteLog($"Arguments: {arguments}");
            WriteLog($"Elevated: {needsElevation}");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = arguments,
                UseShellExecute = false, // Use CreateProcess to inherit admin privileges
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            Console.WriteLine($"     🔧 Running PowerShell: {arguments}");
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                // Capture output for debugging
                if (startInfo.RedirectStandardOutput)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        WriteLog($"PowerShell output: {output}");
                    }
                }
                
                if (startInfo.RedirectStandardError)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        WriteLog($"PowerShell error: {error}");
                    }
                }
                
                WriteLog($"PowerShell script completed with exit code: {process.ExitCode}");
                
                if (process.ExitCode != 0)
                {
                    throw new Exception($"PowerShell script failed with exit code: {process.ExitCode}");
                }
            }
        }
        
        static bool RequiresElevation(string scriptPath, JsonElement packageInfo)
        {
            // Get the script filename to check for known patterns
            string scriptFileName = Path.GetFileName(scriptPath).ToLowerInvariant();
            
            // Get package name/ID for specific package checks
            string packageName = "";
            if (packageInfo.TryGetProperty("name", out var nameProp))
            {
                packageName = nameProp.GetString()?.ToLowerInvariant() ?? "";
            }
            
            string packageId = "";
            if (packageInfo.TryGetProperty("packageid", out var idProp))
            {
                packageId = idProp.GetString()?.ToLowerInvariant() ?? "";
            }
            
            // Scripts that definitely need elevation
            if (scriptFileName.Contains("chocolatey") || 
                scriptFileName.Contains("install-chocolatey") ||
                packageName.Contains("chocolatey") ||
                packageId.Contains("chocolatey"))
            {
                return true;
            }
            
            // Any script that installs system-wide components needs elevation
            if (scriptFileName.Contains("install") && 
                (scriptFileName.Contains("system") || scriptFileName.Contains("global")))
            {
                return true;
            }
            
            // Package manager installers typically need elevation
            if (packageName.Contains("package manager") || 
                packageId.Contains("package-manager"))
            {
                return true;
            }
            
            return false;
        }
        
        static async Task RunMsiInstaller(string msiPath, JsonElement packageInfo)
        {
            var args = GetArguments(packageInfo);
            string arguments = $"/i \"{msiPath}\" /quiet /norestart {string.Join(" ", args)}";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = arguments,
                UseShellExecute = false, // Inherit admin privileges from parent process
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            WriteLog($"Running MSI installer: {arguments}");
            Console.WriteLine($"     📦 Running MSI installer: {arguments}");
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                WriteLog($"MSI installer completed with exit code: {process.ExitCode}");
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
            
            WriteLog($"Running executable: {exePath} {arguments}");
            Console.WriteLine($"     🔧 Running executable: {exePath} {arguments}");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,  // Inherit admin privileges from parent process
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                
                // Capture output for debugging
                if (startInfo.RedirectStandardOutput)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        WriteLog($"Executable output: {output}");
                    }
                }
                
                if (startInfo.RedirectStandardError)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        WriteLog($"Executable error: {error}");
                    }
                }
                
                WriteLog($"Executable completed with exit code: {process.ExitCode}");
                
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
                RedirectStandardError = true,
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
            
            // Install the nupkg - inherit admin privileges from parent process
            string arguments = $"install \"{nupkgPath}\" -y --ignore-checksums {string.Join(" ", args)}";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "choco.exe",
                Arguments = arguments,
                UseShellExecute = false, // Inherit admin privileges from parent process
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            WriteLog($"Running Chocolatey: {arguments}");
            Console.WriteLine($"     🍫 Running Chocolatey: {arguments}");
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                WriteLog($"Chocolatey completed with exit code: {process.ExitCode}");
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
