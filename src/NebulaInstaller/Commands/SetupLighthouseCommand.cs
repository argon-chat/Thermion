namespace Nebuctl.Commands;

using Spectre.Console.Cli;
using System.Net;
using System.Numerics;
using YamlDotNet.Serialization.NamingConventions;

public class SetupLighthouseOptions : CommandSettings, IRemoteCommandOptions
{
    public required string Address { get; set; }

    public required string SubsetCidr { get; set; }
    public required string GatewayAddress { get; set; }
}

public class SetupLighthouseCommand : AsyncRemoteCommand<SetupLighthouseOptions>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SetupLighthouseOptions settings)
    {
        using var client = this.CreateSshClient(settings);
        using var ftp = this.CreateSftpClient(settings);

        await client.ConnectAsync(CancellationToken.None);
        await ftp.ConnectAsync(CancellationToken.None);

        await client.RunCommand("mkdir -p /etc/nebula").ExecuteAsync();

        ftp.WriteAllText("/etc/nebula/ca.crt", await File.ReadAllTextAsync("ca.crt"));
        ftp.WriteAllText("/etc/nebula/lighthouse.crt", await File.ReadAllTextAsync("lighthouse.crt"));
        ftp.WriteAllText("/etc/nebula/lighthouse.key", await File.ReadAllTextAsync("lighthouse.key"));

        var configDir = new DirectoryInfo("/etc/nebula");

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        ftp.WriteAllText("/etc/nebula/lighthouse.yaml", serializer.Serialize(
            GenerateLighthouseConfig(configDir,
                new NebulaDevice("nebula0", IPNetwork2.Parse("240.0.0.0/4"), IPAddress.Parse("188.225.86.188"),
                    9999))));
        return 0;
    }


    private NebulaConfig GenerateLighthouseConfig(DirectoryInfo configDir, NebulaDevice device) =>
        new()
        {
            Pki = new Pki
            {
                Ca = configDir.File("ca.crt"),
                Cert = configDir.File("lighthouse.crt"),
                Key = configDir.File("lighthouse.key")
            },
            Lighthouse = new Lighthouse
            {
                AmLighthouse = true,
                Interval = 60,
                ServeDns = true,
                Dns = new NebulaDns()
                {
                    Host = IPAddress.Any.ToString(),
                    Port = 53
                }
            },
            Listen = new Listen
            {
                Host = IPAddress.Any.ToString(),
                Port = device.port
            },
            Tun = new Tun
            {
                Dev = device.devName,
                Cidr = device.cidr.ToString()
            },
            Firewall = new Firewall
            {
                Conntrack = true,
                Outbound =
                [
                    new FirewallRule
                    {
                        Proto = "any",
                        Port = "any",
                        Host = "any"
                    }
                ],
                Inbound =
                [
                    new FirewallRule
                    {
                        Proto = "any",
                        Port = "any",
                        Host = "any"
                    }
                ]
            }
        };

   
}

public record NebulaDevice(string devName, IPNetwork2 cidr, IPAddress publicGateway, int port = 4242)
{
    public static NebulaDevice Default(IPNetwork2 cidr, IPAddress publicGateway) =>
        new NebulaDevice("nebula1", cidr, publicGateway);
}