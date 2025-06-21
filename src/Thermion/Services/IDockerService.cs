namespace Thermion.Services;

using Docker.DotNet;
using Docker.DotNet.Models;
using System.Net;
using System.Net.Sockets;

public interface IDockerService
{
    Task<SystemInfoResponse> GetDockerInfoAsync();
    Task<bool> ScaleServiceAsync(string serviceName, ulong replicas);
}


public class DockerService(ILogger<IDockerService> logger) : IDockerService
{
    private readonly DockerClient client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

    public async Task<SystemInfoResponse> GetDockerInfoAsync()
    {
        return await client.System.GetSystemInfoAsync();
    }

    public async Task<bool> ScaleServiceAsync(string serviceName, ulong replicas)
    {
        var service = await client.Swarm.InspectServiceAsync(serviceName);
        var version = service.Version.Index;

        service.Spec.Mode.Replicated.Replicas = replicas;

        var response = await client.Swarm.UpdateServiceAsync(serviceName, new ServiceUpdateParameters
        {
            Service = service.Spec,
            Version = (long)version
        });

        return response.Warnings == null;
    }

    public async Task ClearAllDockers()
    {
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true });
        var self = await client.System.GetSystemInfoAsync();

        foreach (var c in containers)
        {
            if (c.Names.Any(name => name.Contains("thermion"))) continue;

            await client.Containers.RemoveContainerAsync(c.ID, new ContainerRemoveParameters
            {
                Force = true,
                RemoveVolumes = true
            });
        }
    }

    public async Task StartConsulAsync(ThermionConfig config)
    {
        if (config.UseCloudflare)

        logger.LogInformation("Pulling Consul image...");
        await client.Images.CreateImageAsync(new ImagesCreateParameters
        {
            FromImage = "consul",
            Tag = "1.16"
        }, null, new Progress<JSONMessage>(m => logger.LogDebug(m.Status)));

        logger.LogInformation("Creating Consul container...");

        var cmd = new List<string>
        {
            "agent",
            "-node", config.ConsulNodeName,
            "-data-dir", "/consul/data",
            "-client", "0.0.0.0",
            "-bind", "127.0.0.1",
            "-retry-join", config.ConsulJoinAddress
        };

        await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = "consul:latest",
            Name = "consul-slave",
            HostConfig = new HostConfig
            {
                NetworkMode = "host",
                Binds = new List<string>
                {
                    "/opt/thermion/consul:/consul/data"
                }
            },
            Cmd = cmd
        });

        await client.Containers.StartContainerAsync("consul-slave", null);
        logger.LogInformation("Consul started and joined to {JoinAddress}", config.ConsulJoinAddress);
    }


    public void GeneratePorts()
    {
        var random = new Random();
        int basePort = random.Next(10000, 20000);
        int minPort = basePort + 1;
        int maxPort = minPort + 99; // 100 портов на instance
        int listeningPort = basePort;

        return;
    }

    string GetHostIp() =>
        Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .First(a => a.AddressFamily == AddressFamily.InterNetwork)
            .ToString();

    public async Task CoTurnStart(ThermionConfig config)
    {
        var random = new Random();
        var basePort = random.Next(10000, 20000);
        var minPort = basePort + 1;
        var maxPort = minPort + 1000;
        var listeningPort = basePort;

        var ip = GetHostIp();

    }
}


public record ThermionConfig
{
    // master config
    public string Region { get; set; }
    public string Zone { get; set; }
    public string Domain { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public required ThermionVault Vault { get; set; }


    public bool UseCloudflare { get; set; }
    public string CloudflareEmail { get; set; }
    public string CloudflareApiToken { get; set; }

    public string CloudflareVaultMount { get; set; }
    public string CloudflareVaultPath { get; set; }

    public string ConsulJoinAddress { get; set; }
    public string ConsulNodeName { get; set; }

    public string IceRealm => $"{Zone}.{Region}.{Domain}";
    public int IcePort { get; set; } = 3478;
}

public record ThermionVault
{
    public required string Address { get; set; }
    public required string RoleId { get; set; } 
    public required string SecretId { get; set; }

    public bool VaultProtectedByCloudflare { get; set; }
    public string? CloudflareZeroTrustToken { get; set; }

    public required string MountPath { get; set; }
}