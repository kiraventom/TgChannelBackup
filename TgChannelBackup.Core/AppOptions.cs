using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TgChannelBackup.Core;

public static class AppOptions
{
    public static JsonSerializerSettings JsonSettings { get; } = new()
    {
        ContractResolver = new PublicPropsIgnoreContractResolver(),
        Formatting = Formatting.Indented,
    };
}

public class PublicPropsIgnoreContractResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        return fields.Select(f => base.CreateProperty(f, memberSerialization)).ToList();
    }
}
