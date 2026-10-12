namespace TomasAI.IFM.Framework.Serialization;

public interface IJsonSerializer
{
    string Serialize(object obj);
    T Deserialize<T>(string content);
    object Deserialize(string content, Type contentType);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the serializer content-type array contract used by existing adapters.")]
    string[] SupportedContentTypes { get; }
    string ContentType { get; set; }
    DataFormat DataFormat { get; }
}
