using Microsoft.Extensions.Logging;
using InstallApplications.Common.Models;
using System.Text.Json;

namespace InstallApplications.Core.Services;

public interface IPackageService
{
    Task<List<Package>> GetPackagesAsync(string repositoryUrl);
    Task<bool> DownloadPackageAsync(Package package, string downloadPath);
    Task<bool> InstallPackageAsync(Package package, string downloadPath);
    Task<bool> VerifyPackageAsync(Package package, string filePath);
}

public class PackageService : IPackageService
{
    private readonly ILogger<PackageService> _logger;
    private readonly HttpClient _httpClient;

    public PackageService(ILogger<PackageService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<List<Package>> GetPackagesAsync(string repositoryUrl)
    {
        try
        {
            _logger.LogInformation("Downloading package manifest from {RepositoryUrl}", repositoryUrl);
            
            var manifestUrl = $"{repositoryUrl.TrimEnd('/')}/manifest.json";
            var response = await _httpClient.GetStringAsync(manifestUrl);
            
            var manifest = JsonSerializer.Deserialize<PackageManifest>(response);
            if (manifest?.Packages == null)
            {
                _logger.LogError("Invalid manifest format or no packages found");
                return new List<Package>();
            }

            _logger.LogInformation("Found {PackageCount} packages in manifest", manifest.Packages.Count);
            return manifest.Packages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download package manifest from {RepositoryUrl}", repositoryUrl);
            return new List<Package>();
        }
    }

    public async Task<bool> DownloadPackageAsync(Package package, string downloadPath)
    {
        try
        {
            _logger.LogInformation("Downloading package {PackageName} from {Url}", package.Name, package.Url);
            
            var fileName = Path.GetFileName(package.Url) ?? $"{package.Name}.pkg";
            var filePath = Path.Combine(downloadPath, fileName);
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(downloadPath);
            
            // Download the file
            using var response = await _httpClient.GetAsync(package.Url);
            response.EnsureSuccessStatusCode();
            
            await using var fileStream = File.Create(filePath);
            await response.Content.CopyToAsync(fileStream);
            
            _logger.LogInformation("Successfully downloaded {PackageName} to {FilePath}", package.Name, filePath);
            
            // Verify hash if provided
            if (!string.IsNullOrEmpty(package.Hash))
            {
                return await VerifyPackageAsync(package, filePath);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download package {PackageName}", package.Name);
            return false;
        }
    }

    public async Task<bool> InstallPackageAsync(Package package, string downloadPath)
    {
        try
        {
            _logger.LogInformation("Installing package {PackageName} (type: {PackageType})", package.Name, package.Type);
            
            var fileName = Path.GetFileName(package.Url) ?? $"{package.Name}.pkg";
            var filePath = Path.Combine(downloadPath, fileName);
            
            if (!File.Exists(filePath))
            {
                _logger.LogError("Package file not found: {FilePath}", filePath);
                return false;
            }

            var success = package.Type.ToLowerInvariant() switch
            {
                "msi" => await InstallMsiAsync(package, filePath),
                "exe" => await InstallExeAsync(package, filePath),
                "ps1" => await InstallPowerShellAsync(package, filePath),
                "nupkg" => await InstallChocolateyAsync(package, filePath),
                "msix" => await InstallMsixAsync(package, filePath),
                _ => throw new NotSupportedException($"Package type '{package.Type}' is not supported")
            };

            if (success)
            {
                _logger.LogInformation("Successfully installed package {PackageName}", package.Name);
            }
            else
            {
                _logger.LogError("Failed to install package {PackageName}", package.Name);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install package {PackageName}", package.Name);
            return false;
        }
    }

    public async Task<bool> VerifyPackageAsync(Package package, string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(package.Hash))
            {
                _logger.LogWarning("No hash provided for package {PackageName}, skipping verification", package.Name);
                return true;
            }

            _logger.LogInformation("Verifying hash for package {PackageName}", package.Name);
            
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            var computedHash = await sha256.ComputeHashAsync(stream);
            var computedHashString = Convert.ToHexString(computedHash);
            
            var expectedHash = package.Hash.Replace("-", "").ToUpperInvariant();
            var isValid = string.Equals(computedHashString, expectedHash, StringComparison.OrdinalIgnoreCase);
            
            if (isValid)
            {
                _logger.LogInformation("Hash verification successful for package {PackageName}", package.Name);
            }
            else
            {
                _logger.LogError("Hash verification failed for package {PackageName}. Expected: {ExpectedHash}, Computed: {ComputedHash}", 
                    package.Name, expectedHash, computedHashString);
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify hash for package {PackageName}", package.Name);
            return false;
        }
    }

    private async Task<bool> InstallMsiAsync(Package package, string filePath)
    {
        var arguments = $"/i \"{filePath}\" /quiet {package.Arguments ?? ""}".Trim();
        return await RunProcessAsync("msiexec.exe", arguments);
    }

    private async Task<bool> InstallExeAsync(Package package, string filePath)
    {
        var arguments = package.Arguments ?? "/S";
        return await RunProcessAsync(filePath, arguments);
    }

    private async Task<bool> InstallPowerShellAsync(Package package, string filePath)
    {
        var arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{filePath}\" {package.Arguments ?? ""}".Trim();
        return await RunProcessAsync("powershell.exe", arguments);
    }

    private async Task<bool> InstallChocolateyAsync(Package package, string filePath)
    {
        var arguments = $"install \"{filePath}\" -y {package.Arguments ?? ""}".Trim();
        return await RunProcessAsync("choco.exe", arguments);
    }

    private async Task<bool> InstallMsixAsync(Package package, string filePath)
    {
        var arguments = $"add-appxpackage \"{filePath}\" {package.Arguments ?? ""}".Trim();
        return await RunProcessAsync("powershell.exe", $"-Command \"{arguments}\"");
    }

    private async Task<bool> RunProcessAsync(string fileName, string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            _logger.LogDebug("Executing: {FileName} {Arguments}", fileName, arguments);

            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogDebug("Process completed successfully. Output: {Output}", output);
                return true;
            }
            else
            {
                _logger.LogError("Process failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute process {FileName} {Arguments}", fileName, arguments);
            return false;
        }
    }
}
