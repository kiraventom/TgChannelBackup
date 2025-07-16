using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TgChannelBackup.Core;

public static class AppOptions
{
    public static JsonSerializerOptions Serializer { get; } = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        {
            Modifiers = { TLMessageModifier }
        }
    };

    private static void TLMessageModifier(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(TL.Message))
            return;
    }
}
