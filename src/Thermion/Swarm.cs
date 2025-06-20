namespace Thermion;

using Docker.DotNet;

public static class Swarm
{
    public static async Task Eta()
    {
        var dockerClient =
            new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"))
            .CreateClient();

        var service = await dockerClient.Swarm.InspectServiceAsync("xuitour");
        var version = service.Version.Index;
    }
}