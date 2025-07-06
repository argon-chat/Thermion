namespace Nebuctl.Commands;

using Renci.SshNet;
using Spectre.Console;
using Spectre.Console.Cli;

public abstract class AsyncRemoteCommand<T> : AsyncCommand<T> where T : CommandSettings, IRemoteCommandOptions
{
    private ConnectionInfo CreateConnection(IRemoteCommandOptions options)
    {
        var (host, user, port) = options.Decompose();
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var keyPath = Path.Combine(homeDir, ".ssh", "id_rsa");

        if (!File.Exists(keyPath))
            throw new FileNotFoundException($"SSH private key not found: {keyPath}");
        var privateKey = new PrivateKeyFile(keyPath);
        var authMethod = new PrivateKeyAuthenticationMethod(user, privateKey);
        return new(host, port, user, authMethod);
    }

    protected SshClient CreateSshClient(IRemoteCommandOptions options) 
        => new(CreateConnection(options));
    protected SftpClient CreateSftpClient(IRemoteCommandOptions options) 
        => new(CreateConnection(options));


    protected async Task ExecuteCommand(string command, SshClient ssh)
    {
        AnsiConsole.MarkupLine($"✏️ [gray]{command.EscapeMarkup()}[/]");

        using var cmd = ssh.CreateCommand(command);

        await cmd.ExecuteAsync();

        if (string.IsNullOrEmpty(cmd.Result))
            return;

        AnsiConsole.MarkupLine($"🔍 \n[gray]{cmd.Result.EscapeMarkup()}[/]");
    }

    protected async Task TransferFileAsync(string targetFile, string sourceFile, SftpClient ftp)
    {
        AnsiConsole.MarkupLine($"🗃 [gray]{targetFile.EscapeMarkup()} <-- {sourceFile.EscapeMarkup()}[/]");
        if (await ftp.ExistsAsync(targetFile)) await ftp.DeleteAsync(targetFile);
        ftp.WriteAllText(targetFile, await File.ReadAllTextAsync(sourceFile));
    }
    protected async Task TransferContentAsync(string targetFile, string data, SftpClient ftp)
    {
        AnsiConsole.MarkupLine($"🚑 [gray]data -> {targetFile.EscapeMarkup()}[/]");
        if (await ftp.ExistsAsync(targetFile)) await ftp.DeleteAsync(targetFile);
        ftp.WriteAllText(targetFile, data);
    }
    public static LinuxDistroFamily DetectDistro(SftpClient sftp)
    {
        if (!sftp.IsConnected)
            throw new InvalidOperationException("SFTP client is not connected.");

        try
        {
            if (sftp.Exists("/etc/os-release"))
            {
                var text = sftp.ReadAllText("/etc/os-release");
                if (text.Contains("ID=debian") || text.Contains("ID=ubuntu") || text.Contains("ID=linuxmint"))
                    return LinuxDistroFamily.DebianLike;
                if (text.Contains("ID=\"rhel\"") || text.Contains("ID=centos") || text.Contains("ID=almalinux") || text.Contains("ID=rocky"))
                    return LinuxDistroFamily.RhelLike;
            }

            if (sftp.Exists("/etc/redhat-release"))
                return LinuxDistroFamily.RhelLike;

            if (sftp.Exists("/etc/debian_version"))
                return LinuxDistroFamily.DebianLike;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]warn[/] distro detection failed: [gray]{ex.Message.EscapeMarkup()}[/]");
        }

        return LinuxDistroFamily.Unknown;
    }

    public static bool BinaryExists(SftpClient sftp, string binary)
    {
        var searchPaths = new[] { "/usr/bin", "/bin", "/usr/local/bin" };

        return searchPaths.Select(path => $"{path}/{binary}").Any(sftp.Exists);
    }

}

public enum LinuxDistroFamily
{
    Unknown,
    DebianLike,
    RhelLike
}