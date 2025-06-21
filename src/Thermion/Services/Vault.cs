namespace Thermion.Services;

using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;

public class VaultProvider(IVaultClient client, ThermionConfig config)
{
    public async Task<CloudflareCredentials> GetCloudflareCredentials(CancellationToken ct = default)
    {
        var secretData = await client.V1.Secrets.KeyValue.V2
            .ReadSecretAsync(path: "cloudflare", mountPoint: config.Vault.MountPath);
        var data = secretData.Data.Data;

        if (data.TryGetValue("token", out var tokenValue) && tokenValue is string token && 
            data.TryGetValue("zone", out var zoneValue) && zoneValue is string zone)
            return new CloudflareCredentials(token, zone);
        throw new Exception($"Invalid configuration of cloudflare, not found key 'token' or 'zone'");
    }

    public async Task<ConsulConfig> GetConsulConfig(CancellationToken ct = default)
    {
        var secretData = await client.V1.Secrets.KeyValue.V2
            .ReadSecretAsync(path: "consul", mountPoint: config.Vault.MountPath);
        var data = secretData.Data.Data;

        if (data.TryGetValue("address", out var zoneValue) && zoneValue is string zone)
            return new ConsulConfig(zone, null);
        throw new Exception($"Invalid configuration of consul, not found key 'address' or 'token'");
    }

    public async Task<CoTurnConfig> GetCoTurnConfig(CancellationToken ct = default)
    {
        var secretData = await client.V1.Secrets.KeyValue.V2
            .ReadSecretAsync(path: "coturn_config", mountPoint: config.Vault.MountPath);
        var data = secretData.Data.Data;

        if (data.TryGetValue("value", out var configValue) && configValue is string value &&
            data.TryGetValue("hmac", out var hmacValue) && hmacValue is string hmac)
            return new CoTurnConfig(value, hmac);
        throw new Exception($"Invalid configuration of coturn, not found key 'value' or 'hmac'");
    }
}

public record CloudflareCredentials(string Token, string ZoneDomain);
public record ConsulConfig(string Address, string? Token);
public record CoTurnConfig(string Config, string Hmac);

public static class VaultProviderExtensions
{
    public static IServiceCollection AddVault(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAuthMethodInfo>(x =>
        {
            var cfg = x.GetRequiredService<ThermionConfig>();
            return new AppRoleAuthMethodInfo(cfg.Vault.RoleId, cfg.Vault.SecretId);
        });
        builder.Services.AddSingleton<VaultClientSettings>(x =>
        {
            var cfg = x.GetRequiredService<ThermionConfig>();
            var authMethod = x.GetRequiredService<IAuthMethodInfo>();

            return new VaultClientSettings(cfg.Vault.Address, authMethod);
        });

        builder.Services.AddSingleton<IVaultClient, VaultClient>();
    }
}