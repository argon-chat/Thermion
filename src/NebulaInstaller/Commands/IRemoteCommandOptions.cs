namespace Nebuctl.Commands;

public interface IRemoteCommandOptions
{
    string Address { get; }


    (string host, string user, int port) Decompose()
    {
        var user = "root";
        var port = 22;

        var hostPart = "";
        var portSeparatorIndex = Address.LastIndexOf(':');

        if (portSeparatorIndex != -1 && portSeparatorIndex > Address.LastIndexOf('@'))
        {
            if (!int.TryParse(Address[(portSeparatorIndex + 1)..], out port))
                throw new FormatException("Cannot parse ssh port");
            hostPart = Address[..portSeparatorIndex];
        }
        else
            hostPart = Address;

        var atIndex = hostPart.IndexOf('@');
        var host = "";
        if (atIndex != -1)
        {
            user = hostPart[..atIndex];
            host = hostPart[(atIndex + 1)..];
        }
        else
            host = hostPart;

        if (string.IsNullOrWhiteSpace(host))
            throw new FormatException("Host required");

        return (host, user, port);
    }
}