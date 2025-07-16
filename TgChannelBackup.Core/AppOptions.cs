using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TL;

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
        if (typeInfo.Type != typeof(Message))
            return;
        
        AddField(typeInfo, nameof(Message.message), "Text");
        AddField(typeInfo, 
                 nameof(Message.fwd_from), 
                 "Forward", 
                 typeof(string), 
                 getter: o => JsonSerializer.Serialize(((Message)o).fwd_from, AppOptions.Serializer),
                 setter: (o, v) => ((Message)o).fwd_from = (MessageFwdHeader)JsonSerializer.Deserialize((string)v, typeof(MessageFwdHeader), AppOptions.Serializer));
    }

    private static void AddField(JsonTypeInfo typeInfo, string propertyName, string jsonPropertyName = null, Type jsonPropertyType = null, Func<object, object> getter = null, Action<object, object> setter = null)
    {
        var fieldInfo = typeof(Message).GetField(propertyName, BindingFlags.Instance);

        var pi = typeInfo.CreateJsonPropertyInfo(jsonPropertyType ?? fieldInfo.FieldType, jsonPropertyName ?? propertyName);
        pi.Get = getter ?? fieldInfo.GetValue;
        pi.Set = setter ?? fieldInfo.SetValue;

        typeInfo.Properties.Add(pi);
    }
}
