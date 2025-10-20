using System.Text.Json;
using System.Text.Json.Serialization;

namespace RattedSystemsCli.HostAPI.SocketUploader;

public class SocketMessage
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "";
    
    [JsonPropertyName("data")]
    public object? Data { get; set; }
    
    public override string ToString()
    {
        return $"\"{Op}\" {(Data != null ? $"({Data.GetType().Name})" : "")} {Data}";
    }
    
    // Typed getter for Data. 
    public T? GetData<T>()
    {
        if (Data is JsonElement jsonElement)
        {
            return jsonElement.Deserialize<T>();
        }
        return (T?)Data;
    }
    
    public T? GetDataProperty<T>(string propertyName)
    {
        JsonElement jsonElement;
        if (Data is JsonElement je)
        {
            jsonElement = je;
        }
        else
        {
            var json = JsonSerializer.Serialize(Data);
            jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
        }
        if (jsonElement.ValueKind == JsonValueKind.Object && jsonElement.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return propertyElement.Deserialize<T>();
        }  
        
        return default;
    }
     
}