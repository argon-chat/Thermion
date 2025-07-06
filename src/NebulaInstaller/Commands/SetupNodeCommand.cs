namespace Nebuctl.Commands;

using Spectre.Console;
using System.ComponentModel;
using System.Net;
using System.Numerics;
using Zx;

public class SetupNodeOptions : CommandSettings, IRemoteCommandOptions
{
    [Description("SSH Address")]
    [CommandArgument(0, "[ssh]")]
    public required string Address { get; set; }

    [Description("Machine Name")]
    [CommandOption("--machine")]
    public string? MachineName { get; set; }
}

public class SetupNodeCommand : AsyncRemoteCommand<SetupNodeOptions>
{
    public const string NebulaBinaries =
        "https://github.com/slackhq/nebula/releases/download/v1.9.5/nebula-linux-amd64.tar.gz";

    public override async Task<int> ExecuteAsync(CommandContext context, SetupNodeOptions settings)
    {
        AnsiConsole.MarkupLine("[blue]>>[/] [bold]Initializing node configuration[/]");
        var configDir = new DirectoryInfo("/etc/nebula");
        var cidr = ReadCidr();


        AnsiConsole.MarkupLine("[green]✓[/] Reading CIDR from configuration... [dim]{0}[/]", cidr);

        var gateway = ReadGateway();

        AnsiConsole.MarkupLine("[green]✓[/] Retrieved gateway... [dim]{0}[/]", gateway);

        var machineId = GetMachineId();

        AnsiConsole.MarkupLine("[green]✓[/] Assigned Machine ID... [dim]{0}[/]", machineId);
        var dev = NebulaDevice.Default(cidr, gateway);

        var cfg = GenerateNodeConfig(configDir, machineId, dev);

        AnsiConsole.MarkupLine("[green]✓[/] Setting up network device... [dim]{0} (CIDR: {1}, GW: {2})[/]", dev.devName, cidr, gateway);

        AnsiConsole.MarkupLine("[green]✓[/] Generating node configuration at [bold]{0}[/]", configDir);


        var machineName = string.IsNullOrEmpty(settings.MachineName) ? await AnsiConsole.AskAsync<string>("Enter MachineName: ") : settings.MachineName;


        using var ssh = this.CreateSshClient(settings);
        using var ftp = this.CreateSftpClient(settings);



        await ssh.ConnectAsync(CancellationToken.None);
        await ftp.ConnectAsync(CancellationToken.None);


        await ExecuteCommand("rm -rf /etc/nebula", ssh);
        await ExecuteCommand("mkdir -p /etc/nebula", ssh);

        await TransferFileAsync(configDir.File("ca.crt"), "ca.crt", ftp);

        if (File.Exists($"{machineName}.crt"))
            File.Delete($"{machineName}.crt");
        if (File.Exists($"{machineName}.key"))
            File.Delete($"{machineName}.key");

        await $"nebula-cert sign -name {machineName} -ip {GetIndexedCidr(dev.cidr, machineId)}";
        AnsiConsole.MarkupLine("[green]✓[/] Generated node certificates for [bold]{0}[/]", machineName);


        await TransferFileAsync(configDir.File($"service.crt"), $"{machineName}.crt", ftp);
        AnsiConsole.MarkupLine("[green]✓[/] service.crt success write [bold]{0}[/]", configDir);

        await TransferFileAsync(configDir.File($"service.key"), $"{machineName}.key", ftp);
        AnsiConsole.MarkupLine("[green]✓[/] service.key success write [bold]{0}[/]", configDir);

        await TransferContentAsync(configDir.File($"service.yaml"), cfg.Serialize(), ftp);
        AnsiConsole.MarkupLine("[green]✓[/] service.yaml success write [bold]{0}[/]", configDir);

        var distro = DetectDistro(ftp);

        AnsiConsole.MarkupLine("[green]✓[/] Detected [bold]{0}[/] distro", distro);

        var pkgManager = distro switch
        {
            LinuxDistroFamily.DebianLike => "apt-get",
            LinuxDistroFamily.RhelLike => "yum",
            _ => throw new Exception("Not supported distro"),
        };

        if (!BinaryExists(ftp, "jq"))
        {
            AnsiConsole.MarkupLine("⚠️ [bold]jq[/] not detected, install...");
            await ExecuteCommand($"{pkgManager} -y install jq", ssh);
        }

        if (!BinaryExists(ftp, "tar"))
        {
            AnsiConsole.MarkupLine("⚠️ [bold]tar[/] not detected, install...");
            await ExecuteCommand($"{pkgManager} -y install tar", ssh);
        }

        if (!BinaryExists(ftp, "curl"))
        {
            AnsiConsole.MarkupLine("⚠️ [bold]curl[/] not detected, install...");
            await ExecuteCommand($"{pkgManager} -y install curl", ssh);
        }

        await ExecuteCommand($"curl -L -o /usr/local/bin/nebula-linux-amd64.tar.gz {NebulaBinaries}", ssh);
        await ExecuteCommand($"tar -xzf /usr/local/bin/nebula-linux-amd64.tar.gz -C /usr/local/bin nebula nebula-cert", ssh);
        await ExecuteCommand($"chmod +x /usr/local/bin/nebula /usr/local/bin/nebula-cert", ssh);
        await ExecuteCommand($"rm /usr/local/bin/nebula-linux-amd64.tar.gz", ssh);

        AnsiConsole.MarkupLine("[green]✓[/] Success download nebula binaries");


        if (!await ftp.ExistsAsync("/etc/systemd/system/nebula.service"))
        {
            await TransferContentAsync("/etc/systemd/system/nebula.service",
                """
                [Unit]
                Description=Nebula
                After=network-online.target

                [Service]
                ExecStart=/usr/local/bin/nebula -config /etc/nebula/service.yaml
                Restart=on-failure
                RestartSec=5
                AmbientCapabilities=CAP_NET_ADMIN CAP_NET_BIND_SERVICE
                CapabilityBoundingSet=CAP_NET_ADMIN CAP_NET_BIND_SERVICE
                LimitNOFILE=65535

                [Install]
                WantedBy=multi-user.target
                """, ftp);

            await ExecuteCommand("systemctl daemon-reload", ssh);
            await ExecuteCommand("systemctl enable nebula", ssh);
            await ExecuteCommand("systemctl start nebula", ssh);
            await ExecuteCommand("systemctl is-active nebula", ssh);
            AnsiConsole.MarkupLine("[green]✓[/] systemd service is configured");
        }
        else
        {
            await ExecuteCommand("systemctl restart nebula", ssh);
            await ExecuteCommand("systemctl is-active nebula", ssh);
            AnsiConsole.MarkupLine("[green]✓[/] systemd service is already configured");
        }

        IncMachineId();

        AnsiConsole.MarkupLine("[yellow]💫[/] Configuration successfully generated and ready to use.");
        return 0;
    }



    private int GetMachineId()
    {
        var text = File.ReadAllText(".next_hid");

        var next = int.Parse(text);

        return next;
    }

    private void IncMachineId()
    {
        var text = File.ReadAllText(".next_hid");

        var next = int.Parse(text);

        File.WriteAllText(".next_hid", $"{next + 1}");
    }

    private IPNetwork2 ReadCidr() => IPNetwork2.Parse(File.ReadAllText(".cidr"));
    private IPAddress ReadGateway() => IPAddress.Parse(File.ReadAllText(".gateway"));

    private NebulaConfig GenerateNodeConfig(DirectoryInfo configDir, int machineId, NebulaDevice device) =>
       new()
       {
           Pki = new()
           {
               Ca = configDir.File("ca.crt"),
               Cert = configDir.File("service.crt"),
               Key = configDir.File("service.key")
           },
           StaticHostMap = new()
           {
                { device.cidr.FirstUsable.ToString(), [$"{device.publicGateway}:{device.port}"] }
           },
           Lighthouse = new()
           {
               AmLighthouse = false,
               Interval = 60,
               Hosts = [device.cidr.FirstUsable.ToString()]
           },
           Tun = new()
           {
               Dev = device.devName,
               Cidr = GetIndexedCidr(device.cidr, machineId)
           },
           Listen = new()
           {
               Host = IPAddress.Any.ToString(),
               Port = device.port,
           },
           Firewall = new()
           {
               Conntrack = true,
               Outbound =
               [
                   new()
                    {
                        Proto = Firewall.Any,
                        Port = Firewall.Any,
                        Host = Firewall.Any
                    }
               ],
               Inbound =
               [
                   new()
                    {
                        Proto = Firewall.Any,
                        Port = Firewall.Any,
                        Host = Firewall.Any
                    }
               ]
           }
       };


    static string GetIndexedCidr(IPNetwork2 network, BigInteger index)
    {
        var start = network.Network;
        var ipBytes = start.GetAddressBytes();
        var isIPv6 = ipBytes.Length == 16;
        var baseInt = new BigInteger(ipBytes.Reverse().Concat(new byte[] { 0 }).ToArray());
        var targetInt = baseInt + index;
        var targetBytes = targetInt.ToByteArray();
        var padded = new byte[isIPv6 ? 16 : 4];
        Array.Copy(targetBytes, 0, padded, 0, Math.Min(padded.Length, targetBytes.Length));
        var resultIp = new IPAddress(padded.Reverse().ToArray());
        return $"{resultIp}/{network.Cidr}";
    }
}