namespace TomasAI.IFM.Framework.Serialization;

public interface IJsonSerializer
{
    string Serialize(object obj);
    T Deserialize<T>(string content);
    object Deserialize(string content, Type contentType);

    string[] SupportedContentTypes { get; } 
    string ContentType { get; set; } 
    DataFormat DataFormat { get; }
}

public enum DataFormat
{
    Json,
    Xml,
    None
}
