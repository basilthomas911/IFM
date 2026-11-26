using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

/// <summary>On-demand proof that captured application assemblies match the code loaded by the original owner.</summary>
public static class SessionCaptureRuntimeEvidence
{
    public sealed record AssemblyEvidence(string RelativePath,Guid ModuleVersionId,string Sha256,long Bytes);
    public static IReadOnlyList<AssemblyEvidence> Capture()
    {
        var root=Path.GetFullPath(AppContext.BaseDirectory);
        var result=new SortedDictionary<string,AssemblyEvidence>(StringComparer.OrdinalIgnoreCase);
        long bytes=0;
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if(assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))continue;
            var path=Path.GetFullPath(assembly.Location);
            var relative=Path.GetRelativePath(root,path);
            if(Path.IsPathRooted(relative)||relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))continue;
            var expected=assembly.ManifestModule.ModuleVersionId;
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            using(var pe=new PEReader(stream,PEStreamOptions.LeaveOpen))
            {
                var metadata=pe.GetMetadataReader();
                if(metadata.GetGuid(metadata.GetModuleDefinition().Mvid)!=expected)
                    throw new InvalidOperationException("A loaded application assembly differs from the current file: "+relative);
            }
            stream.Position=0;
            var value=new AssemblyEvidence(relative,expected,Convert.ToHexStringLower(SHA256.HashData(stream)),stream.Length);
            if(result.TryGetValue(relative,out var prior))
            {if(prior!=value)throw new InvalidOperationException("Loaded assembly contexts disagree: "+relative);continue;}
            if(result.Count>=1024 || (bytes+=stream.Length)>2L*1024*1024*1024)throw new InvalidDataException("Application runtime evidence exceeds its finite bound.");
            result.Add(relative,value);
        }
        if(result.Count==0)throw new InvalidDataException("No original application assemblies were observed.");
        return result.Values.ToArray();
    }
}
