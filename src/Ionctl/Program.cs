using Ionctl;
using Ionctl.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    System.Console.OutputEncoding = Encoding.Unicode;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(x => x.SetMinimumLevel(LogLevel.None))
    .UseConsoleLifetime()
    .UseSpectreConsole(config =>
    {
        config.SetApplicationCulture(CultureInfo.InvariantCulture);
        config.SetApplicationName("ionctl");

        config.AddCommand<SetupCommand>("setup")
            .WithDescription("Setup Thermion")
            .WithAlias("start")
            .WithAlias("gotagofast")
            .WithAlias("begin");
    })
    .RunConsoleAsync();

return Environment.ExitCode;