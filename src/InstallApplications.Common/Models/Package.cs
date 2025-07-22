using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InstallApplications.Common.Models;

public class PackageManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("packages")]
    public List<Package> Packages { get; set; } = new();

    [JsonPropertyName("settings")]
    public PackageSettings? Settings { get; set; }
}

public class Package
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    [Required]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    [Required]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "setupassistant";

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("conditions")]
    public PackageConditions? Conditions { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; } = 0;

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 3600;

    [JsonPropertyName("retries")]
    public int Retries { get; set; } = 3;
}

public class PackageConditions
{
    [JsonPropertyName("os_version")]
    public string? OsVersion { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("domain_joined")]
    public bool? DomainJoined { get; set; }

    [JsonPropertyName("registry_key")]
    public string? RegistryKey { get; set; }

    [JsonPropertyName("registry_value")]
    public string? RegistryValue { get; set; }

    [JsonPropertyName("file_exists")]
    public string? FileExists { get; set; }

    [JsonPropertyName("service_exists")]
    public string? ServiceExists { get; set; }
}

public class PackageSettings
{
    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 3600;

    [JsonPropertyName("retries")]
    public int Retries { get; set; } = 3;

    [JsonPropertyName("cleanup")]
    public bool Cleanup { get; set; } = true;

    [JsonPropertyName("reboot_required")]
    public bool RebootRequired { get; set; } = false;

    [JsonPropertyName("log_level")]
    public string LogLevel { get; set; } = "Information";

    [JsonPropertyName("download_path")]
    public string DownloadPath { get; set; } = @"C:\ProgramData\InstallApplications\Downloads";

    [JsonPropertyName("progress_ui")]
    public bool ProgressUI { get; set; } = true;
}

public enum InstallationPhase
{
    SetupAssistant,
    Userland
}

public enum PackageStatus
{
    Pending,
    Downloading,
    Installing,
    Completed,
    Failed,
    Skipped
}
