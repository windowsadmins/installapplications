using CommandLine;

namespace InstallApplications.Common.Options;

[Verb("install", HelpText = "Install packages from repository")]
public class InstallOptions
{
    [Option('r', "repo", Required = true, HelpText = "Package repository URL")]
    public string RepositoryUrl { get; set; } = string.Empty;

    [Option('p', "phase", Default = "setupassistant", HelpText = "Installation phase (setupassistant, userland)")]
    public string Phase { get; set; } = "setupassistant";

    [Option('c', "config", HelpText = "Custom configuration file path")]
    public string? ConfigFile { get; set; }

    [Option('d', "dry-run", Default = false, HelpText = "Test mode without actual installation")]
    public bool DryRun { get; set; }

    [Option('v', "verbose", Default = false, HelpText = "Enable verbose logging")]
    public bool Verbose { get; set; }

    [Option("download-only", Default = false, HelpText = "Download packages without installing")]
    public bool DownloadOnly { get; set; }

    [Option("continue-on-error", Default = false, HelpText = "Continue installation even if non-required packages fail")]
    public bool ContinueOnError { get; set; }
}

[Verb("service", HelpText = "Service management operations")]
public class ServiceOptions
{
    [Option("install", Group = "action", HelpText = "Install the Windows service")]
    public bool Install { get; set; }

    [Option("uninstall", Group = "action", HelpText = "Uninstall the Windows service")]
    public bool Uninstall { get; set; }

    [Option("start", Group = "action", HelpText = "Start the Windows service")]
    public bool Start { get; set; }

    [Option("stop", Group = "action", HelpText = "Stop the Windows service")]
    public bool Stop { get; set; }

    [Option("status", Group = "action", HelpText = "Show service status")]
    public bool Status { get; set; }
}

[Verb("bootstrap", HelpText = "Bootstrap deployment - install service and start")]
public class BootstrapOptions
{
    [Option('r', "repo", Required = true, HelpText = "Package repository URL")]
    public string RepositoryUrl { get; set; } = string.Empty;

    [Option('c', "config", HelpText = "Custom configuration file path")]
    public string? ConfigFile { get; set; }

    [Option("auto-start", Default = true, HelpText = "Automatically start installation after service setup")]
    public bool AutoStart { get; set; }

    [Option("cleanup", Default = true, HelpText = "Remove installer after successful bootstrap")]
    public bool Cleanup { get; set; }
}

[Verb("validate", HelpText = "Validate package manifest")]
public class ValidateOptions
{
    [Option('r', "repo", Required = true, HelpText = "Package repository URL")]
    public string RepositoryUrl { get; set; } = string.Empty;

    [Option('f', "file", HelpText = "Local manifest file to validate")]
    public string? ManifestFile { get; set; }

    [Option("check-urls", Default = false, HelpText = "Verify all package URLs are accessible")]
    public bool CheckUrls { get; set; }

    [Option("check-hashes", Default = false, HelpText = "Verify package hashes")]
    public bool CheckHashes { get; set; }
}
