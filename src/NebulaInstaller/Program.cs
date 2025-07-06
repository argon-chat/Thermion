using Ionctl;
using Nebuctl.Commands;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    Console.OutputEncoding = Encoding.Unicode;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(x => x.SetMinimumLevel(LogLevel.None))
    .UseConsoleLifetime()
    .UseSpectreConsole(config => {
        config.SetApplicationCulture(CultureInfo.InvariantCulture);
        config.SetApplicationName("nebuctl");

        config.AddBranch("setup", q =>
        {
            q.AddCommand<SetupLighthouseCommand>("lighthouse");
            q.AddCommand<SetupNodeCommand>("node");
        });
    })
    .RunConsoleAsync();

return Environment.ExitCode;