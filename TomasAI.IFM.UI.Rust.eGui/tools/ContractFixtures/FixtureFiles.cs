using System.Text;

/// <summary>
/// Keep deterministic fixture timestamps stable. Rewriting unchanged include_bytes
/// inputs forces Cargo to rebuild and relink every dependent desktop test.
/// This development helper does not alter the exported bytes or production I/O.
/// </summary>
internal static class FixtureFiles
{
    public static void WriteAllBytes(string path,byte[] value)
    {
        if(File.Exists(path)&&File.ReadAllBytes(path).AsSpan().SequenceEqual(value))return;
        File.WriteAllBytes(path,value);
    }
    public static void WriteAllText(string path,string value)=>WriteAllBytes(path,Encoding.UTF8.GetBytes(value));
}
