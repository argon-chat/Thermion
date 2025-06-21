namespace Ionctl.Commands;

using Docker.DotNet;
using Docker.DotNet.Models;
using Newtonsoft.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;

public class SetupCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        AnsiConsole.Write(
            new FigletText("Thermion")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[gray]Interactive setup for the Thermion control node[/]");
        AnsiConsole.WriteLine();

        var config = new ThermionConfig();

        AnsiConsole.MarkupLine("[bold underline]🗺️  Region and Location[/]");

        config.Region = AnsiConsole.Ask<string>("Region (e.g. [green]eu[/])?");
        config.Zone = AnsiConsole.Ask<string>("Zone (e.g. [green]zone-1a[/])?");

        config.Domain = AnsiConsole.Prompt(
            new TextPrompt<string>("Base domain (e.g. [green]turn.example.org[/])?")
                .PromptStyle("green")
                .ValidationErrorMessage("[red]Invalid domain[/]")
                .Validate(domain => domain.Contains('.') ? ValidationResult.Success() : ValidationResult.Error("Must be a valid domain")));

        AnsiConsole.MarkupLine("[gray]ICE node hostnames will be generated like [blue]{zone}.{domain}[/].[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold underline]🌐 Location Detection[/]");

        try
        {
            var client = new HttpClient();
            var ipinfo = await client.GetFromJsonAsync<IpInfoResponse>("https://ipinfo.io/json");

            if (ipinfo?.loc is not null && ipinfo.loc.Contains(','))
            {
                var parts = ipinfo.loc.Split(',');
                config.Latitude = double.Parse(parts[0]);
                config.Longitude = double.Parse(parts[1]);

                AnsiConsole.MarkupLine($"[green]Detected location:[/] {ipinfo.city}, {ipinfo.country} ({config.Latitude}, {config.Longitude})");

                if (!await AnsiConsole.ConfirmAsync("Use detected location?", true))
                {
                    PromptManualLocation(config);
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Could not determine location automatically.[/]");
                PromptManualLocation(config);
            }
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine("[red]Failed to query ipinfo.io.[/] Falling back to manual input.");
            PromptManualLocation(config);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold underline]🔐 Vault Integration[/]");

        config.UseVault = await AnsiConsole.ConfirmAsync("Use Vault to manage shared TURN secrets?", true);

        if (config.UseVault)
        {
            bool pullCfCreds = await AnsiConsole.ConfirmAsync("Pull Cloudflare credentials from Vault?", true);
            if (pullCfCreds)
            {
                config.CloudflareVaultMount = AnsiConsole.Ask<string>("Vault KV mount (e.g. [green]secret[/])?");
                config.CloudflareVaultPath = AnsiConsole.Ask<string>("Path inside KV (e.g. [green]infra/cloudflare[/])?");
            }
            else
            {
                await AskForCloudflareCreds(config);
            }
        }
        else
        {
            await AskForCloudflareCreds(config);
        }

        var HasDocker = File.Exists("/var/run/docker.sock");
        if (!HasDocker)
        {
            if (await AnsiConsole.ConfirmAsync("Docker is not installed. Install it now?", true))
            {
                await InstallDockerViaScript();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Docker is required for Thermion. Aborting.[/]");
                return -1;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]✅ Configuration complete![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[gray]Launching the agent...[/]");

        await RunThermionImageInDocker(config);

        return 0;
    }

    private async Task InstallDockerViaScript()
    {
        var distro = await DetectDistroAsync();

        if (!distro.Contains("debian", StringComparison.OrdinalIgnoreCase) &&
            !distro.Contains("ubuntu", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]Docker installation via script is only supported on Debian-based systems.[/]");
            AnsiConsole.MarkupLine("[yellow]Please install Docker manually for your distribution (e.g. using dnf/yum).[/]");
            AnsiConsole.MarkupLine($"Detected distro: [blue]{distro}[/]");
            return;
        }

        AnsiConsole.MarkupLine("[yellow]Installing Docker via get.docker.com...[/]");

        var scriptCommand = File.Exists("/usr/bin/curl")
            ? "curl -fsSL https://get.docker.com | bash"
            : "wget -qO- https://get.docker.com | bash";

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{scriptCommand}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = Process.Start(psi);
        if (process is null)
        {
            AnsiConsole.MarkupLine("[red]Failed to start Docker install process.[/]");
            return;
        }

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            AnsiConsole.MarkupLine("[green]Docker installed successfully.[/]");
        }
        else
        {
            var error = await process.StandardError.ReadToEndAsync();
            AnsiConsole.MarkupLine($"[red]Docker install failed (exit code {process.ExitCode}):[/]\n{error}");
        }
    }

    private async Task<string> DetectDistroAsync()
    {
        try
        {
            var osRelease = await File.ReadAllTextAsync("/etc/os-release");
            var lines = osRelease.Split('\n');

            var idLine = lines.FirstOrDefault(l => l.StartsWith("ID=", StringComparison.OrdinalIgnoreCase));
            if (idLine != null)
            {
                return idLine.Split('=')[1].Trim('"', '\'');
            }
        }
        catch
        {
            // ignore and return unknown
        }

        return "unknown";
    }

    private void PromptManualLocation(ThermionConfig config)
    {
        config.Latitude = AnsiConsole.Prompt(
            new TextPrompt<double>("Latitude:")
                .Validate(lat => lat is >= -90 and <= 90 ? ValidationResult.Success() : ValidationResult.Error("Must be between -90 and 90")));

        config.Longitude = AnsiConsole.Prompt(
            new TextPrompt<double>("Longitude:")
                .Validate(lng => lng is >= -180 and <= 180 ? ValidationResult.Success() : ValidationResult.Error("Must be between -180 and 180")));
    }

    private async Task AskForCloudflareCreds(ThermionConfig config)
    {
        config.UseCloudflare = await AnsiConsole.ConfirmAsync("Use Cloudflare to register DNS?");
        if (config.UseCloudflare)
        {
            config.CloudflareEmail = AnsiConsole.Ask<string>("Cloudflare email:");
            config.CloudflareApiToken = AnsiConsole.Prompt(
                new TextPrompt<string>("Cloudflare API token:").Secret());
        }
    }


    private async Task RunThermionImageInDocker(ThermionConfig cfg)
    {
        var docker = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();
        await docker.Images.CreateImageAsync(new ImagesCreateParameters
        {
            FromImage = "argonchat/thermion",
            Tag = "latest"
        }, null, new Progress<JSONMessage>());

        var envs = new List<string>
        {
            $"THERMION_CFG=\"{Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(cfg)))}\"",
        };

        await docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = "argonchat/thermion:latest",
            Name = "thermion",
            Env = envs,
            HostConfig = new HostConfig
            {
                NetworkMode = "host",
                Binds = new List<string> { "/var/run/docker.sock:/var/run/docker.sock" }
            }
        });
        await docker.Containers.StartContainerAsync("thermion", null);
    }

    private record IpInfoResponse
    {
        public required string ip { get; init; }
        public required string city { get; init; }
        public required string country { get; init; }
        public required string loc { get; init; }
    }
}


public record ThermionConfig
{
    public string Region { get; set; }
    public string Zone { get; set; }
    public string Domain { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public bool UseVault { get; set; }
    public string VaultAddress { get; set; }
    public bool VaultProtectedByCloudflare { get; set; }
    public string CloudflareZeroTrustToken { get; set; }

    public bool UseCloudflare { get; set; }
    public string CloudflareEmail { get; set; }
    public string CloudflareApiToken { get; set; }

    public string CloudflareVaultMount { get; set; }
    public string CloudflareVaultPath { get; set; }
}