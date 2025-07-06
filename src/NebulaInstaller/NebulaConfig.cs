#nullable disable

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.TypeInspectors;

public class NebulaConfig
{
    [YamlMember(Alias = "pki")]
    public Pki Pki { get; set; }

    [YamlMember(Alias = "static_host_map")]
    public Dictionary<string, List<string>> StaticHostMap { get; set; }

    [YamlMember(Alias = "lighthouse")]
    public Lighthouse Lighthouse { get; set; }

    [YamlMember(Alias = "listen")]
    public Listen Listen { get; set; }

    [YamlMember(Alias = "punchy")]
    public Punchy Punchy { get; set; }

    [YamlMember(Alias = "relay")]
    public Relay Relay { get; set; }

    [YamlMember(Alias = "tun")]
    public Tun Tun { get; set; }

    [YamlMember(Alias = "logging")]
    public Logging Logging { get; set; }

    [YamlMember(Alias = "firewall")]
    public Firewall Firewall { get; set; }

    public string Serialize()
    {
        var serializer = new SerializerBuilder()
            .WithTypeInspector(inspector => new NullExcludingTypeInspector(inspector))
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull) // safety net
            .WithQuotingNecessaryStrings() // кавычки вокруг IP/строк с точками
            .WithTypeConverter(new DictionaryFlowStyleConverter())
            .Build();
        return serializer.Serialize(this);
    }
}

public class DictionaryFlowStyleConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) =>
        type == typeof(Dictionary<string, List<string>>);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        throw new NotImplementedException();
    }

    public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
    {
        var dict = (Dictionary<string, List<string>>)value;

        emitter.Emit(new MappingStart());

        foreach (var kvp in dict)
        {
            // Ключ — всегда в двойных кавычках
            emitter.Emit(new Scalar(null, null, kvp.Key, ScalarStyle.DoubleQuoted, true, false));

            // Значение — сериализуем как flow-style список ["addr"]
            emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Flow));
            foreach (var item in kvp.Value)
            {
                emitter.Emit(new Scalar(item));
            }
            emitter.Emit(new SequenceEnd());
        }

        emitter.Emit(new MappingEnd());
    }

    public object ReadYaml(IParser parser, Type type)
    {
        throw new NotImplementedException("Reading not implemented.");
    }

    public void WriteYaml(IEmitter emitter, object value, Type type)
    {
        var dict = (Dictionary<string, List<string>>)value;
        emitter.Emit(new MappingStart());

        foreach (var kv in dict)
        {
            emitter.Emit(new Scalar(null, null, kv.Key, ScalarStyle.DoubleQuoted, true, false));
            emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Flow));
            foreach (var item in kv.Value)
            {
                emitter.Emit(new Scalar(null, null, item, ScalarStyle.Any, true, false));
            }
            emitter.Emit(new SequenceEnd());
        }

        emitter.Emit(new MappingEnd());
    }
}
public class NullExcludingTypeInspector(ITypeInspector innerTypeInspector) : TypeInspectorSkeleton
{
    public override string GetEnumName(Type enumType, string name) =>
        (innerTypeInspector as TypeInspectorSkeleton)?.GetEnumName(enumType, name)
        ?? name;

    public override string GetEnumValue(object enumValue) =>
        (innerTypeInspector as TypeInspectorSkeleton)?.GetEnumValue(enumValue)
        ?? enumValue?.ToString();

    public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object container)
    {
        return innerTypeInspector
            .GetProperties(type, container)
            .Where(p => {
                if (container == null) return true;
                var val = p.Read(container);
                return val != null;
            });
    }
}

public class Pki
{
    [YamlMember(Alias = "ca")]
    public string Ca { get; set; }

    [YamlMember(Alias = "cert")]
    public string Cert { get; set; }

    [YamlMember(Alias = "key")]
    public string Key { get; set; }

    [YamlMember(Alias = "blocklist")]
    public List<string> Blocklist { get; set; }

    [YamlMember(Alias = "disconnect_invalid")]
    public bool? DisconnectInvalid { get; set; }

    [YamlMember(Alias = "initiating_version")]
    public int? InitiatingVersion { get; set; }
}

public class Lighthouse
{
    [YamlMember(Alias = "am_lighthouse")] public bool AmLighthouse { get; set; }

    [YamlMember(Alias = "serve_dns")] public bool? ServeDns { get; set; }

    [YamlMember(Alias = "interval")] public int Interval { get; set; }

    [YamlMember(Alias = "hosts")] public List<string> Hosts { get; set; }

    [YamlMember(Alias = "remote_allow_list")]
    public Dictionary<string, bool> RemoteAllowList { get; set; }

    [YamlMember(Alias = "remote_allow_ranges")]
    public Dictionary<string, Dictionary<string, bool>> RemoteAllowRanges { get; set; }

    [YamlMember(Alias = "local_allow_list")]
    public LocalAllowList LocalAllowList { get; set; }

    [YamlMember(Alias = "advertise_addrs")]
    public List<string> AdvertiseAddrs { get; set; }

    [YamlMember(Alias = "calculated_remotes")]
    public Dictionary<string, List<CalculatedRemote>> CalculatedRemotes { get; set; }

    [YamlMember(Alias = "dns")]
    public NebulaDns Dns { get; set; }

}

public class NebulaDns
{
    [YamlMember(Alias = "host")]
    public string Host { get; set; }
    [YamlMember(Alias = "port")]
    public int Port { get; set; }
}

public class LocalAllowList
{
    [YamlMember(Alias = "interfaces")]
    public Dictionary<string, bool> Interfaces { get; set; }

    [YamlMember(Alias = "cidr")]
    public Dictionary<string, bool> Cidr { get; set; }
}

public class CalculatedRemote
{
    [YamlMember(Alias = "mask")]
    public string Mask { get; set; }

    [YamlMember(Alias = "port")]
    public int Port { get; set; }
}

public class Listen
{
    [YamlMember(Alias = "host")]
    public string Host { get; set; }

    [YamlMember(Alias = "port")]
    public int Port { get; set; }

    [YamlMember(Alias = "batch")]
    public int? Batch { get; set; }

    [YamlMember(Alias = "read_buffer")]
    public int? ReadBuffer { get; set; }

    [YamlMember(Alias = "write_buffer")]
    public int? WriteBuffer { get; set; }

    [YamlMember(Alias = "send_recv_error")]
    public string SendRecvError { get; set; }

    [YamlMember(Alias = "so_mark")]
    public int? SoMark { get; set; }
}

public class Punchy
{
    [YamlMember(Alias = "punch")]
    public bool Punch { get; set; }

    [YamlMember(Alias = "respond")]
    public bool? Respond { get; set; }

    [YamlMember(Alias = "delay")]
    public string Delay { get; set; }

    [YamlMember(Alias = "respond_delay")]
    public string RespondDelay { get; set; }
}


public class Relay
{
    [YamlMember(Alias = "relays")]
    public List<string> Relays { get; set; }

    [YamlMember(Alias = "am_relay")]
    public bool AmRelay { get; set; }

    [YamlMember(Alias = "use_relays")]
    public bool UseRelays { get; set; }
}

public class Tun
{
    [YamlMember(Alias = "disabled")]
    public bool Disabled { get; set; }

    [YamlMember(Alias = "dev")]
    public string Dev { get; set; }

    [YamlMember(Alias = "cidr")]
    public string Cidr { get; set; }

    [YamlMember(Alias = "drop_local_broadcast")]
    public bool? DropLocalBroadcast { get; set; }

    [YamlMember(Alias = "drop_multicast")]
    public bool? DropMulticast { get; set; }

    [YamlMember(Alias = "tx_queue")]
    public int? TxQueue { get; set; }

    [YamlMember(Alias = "mtu")]
    public int? Mtu { get; set; }

    [YamlMember(Alias = "routes")]
    public List<RouteMtu> Routes { get; set; }

    [YamlMember(Alias = "unsafe_routes")]
    public List<UnsafeRoute> UnsafeRoutes { get; set; }
}

public class RouteMtu
{
    [YamlMember(Alias = "mtu")]
    public int Mtu { get; set; }

    [YamlMember(Alias = "route")]
    public string Route { get; set; }
}

public class UnsafeRoute
{
    [YamlMember(Alias = "route")]
    public string Route { get; set; }

    [YamlMember(Alias = "via")]
    public object Via { get; set; } // string or list with weights

    [YamlMember(Alias = "mtu")]
    public int? Mtu { get; set; }

    [YamlMember(Alias = "metric")]
    public int? Metric { get; set; }

    [YamlMember(Alias = "install")]
    public bool? Install { get; set; }
}


public class Logging
{
    [YamlMember(Alias = "level")]
    public string Level { get; set; }

    [YamlMember(Alias = "format")]
    public string Format { get; set; }

    [YamlMember(Alias = "disable_timestamp")]
    public bool? DisableTimestamp { get; set; }

    [YamlMember(Alias = "timestamp_format")]
    public string TimestampFormat { get; set; }
}

public class Firewall
{
    public static readonly string Any = "any";

    [YamlMember(Alias = "outbound_action")]
    public string OutboundAction { get; set; }

    [YamlMember(Alias = "inbound_action")]
    public string InboundAction { get; set; }

    [YamlMember(Alias = "conntrack")]
    public bool Conntrack { get; set; }

    [YamlMember(Alias = "outbound")]
    public List<FirewallRule> Outbound { get; set; }

    [YamlMember(Alias = "inbound")]
    public List<FirewallRule> Inbound { get; set; }
}

public class FirewallRule
{
    [YamlMember(Alias = "port")]
    public string Port { get; set; }

    [YamlMember(Alias = "proto")]
    public string Proto { get; set; }

    [YamlMember(Alias = "host")]
    public string Host { get; set; }

    [YamlMember(Alias = "group")]
    public string Group { get; set; }

    [YamlMember(Alias = "groups")]
    public List<string> Groups { get; set; }

    [YamlMember(Alias = "local_cidr")]
    public string LocalCidr { get; set; }
}
