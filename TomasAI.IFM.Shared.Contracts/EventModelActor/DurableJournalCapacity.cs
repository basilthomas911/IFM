namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Bounds actual journal history and reserves two frame slots per pending input, including its future settlement rewrite.</summary>
/// <remarks>This changes no journal format and never deletes or expires evidence. Callers retain their existing exclusive journal owner.</remarks>
public sealed class DurableJournalCapacity
{
    public const long DefaultMaximumBytes=8L*1024*1024*1024;
    readonly string root,pendingRoot;
    readonly long maximumBytes,maximumFrame;
    readonly Lock gate=new();
    readonly Dictionary<string,long> files=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,(string Path,int Length)> settlementFiles=new(StringComparer.OrdinalIgnoreCase);
    long used,reserved;
    long AuxiliaryReserve()=>settlementFiles.Values.Distinct().Sum(value=>Math.Max(0,value.Length-KnownLength(value.Path)));
    Exception? failure;
    public DurableJournalCapacity(string directory,long maximumBytes,long maximumFrameBytes)
    {
        if(maximumBytes<1)throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if(maximumFrameBytes<1||maximumFrameBytes>long.MaxValue/2)throw new ArgumentOutOfRangeException(nameof(maximumFrameBytes));
        root=Path.GetFullPath(directory);pendingRoot=Path.Combine(root,"pending")+Path.DirectorySeparatorChar;
        this.maximumBytes=maximumBytes;maximumFrame=maximumFrameBytes;RefreshAll();RequireCapacity(0);
    }
    public static long ConfiguredMaximum(string variable)
    {
        var text=Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(text)?DefaultMaximumBytes:
            long.TryParse(text,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var value)&&value>0?value:
            throw new InvalidOperationException(variable+" must be a positive finite byte count.");
    }
    public long MaximumBytes=>maximumBytes;
    public int TrackedPathCount{get{lock(gate)return files.Count;}}
    public long RetainedBytes{get{lock(gate)return used;}}
    public long SettlementReservedBytes{get{lock(gate)return reserved+AuxiliaryReserve();}}
    bool Pending(string path)=>path.StartsWith(pendingRoot,StringComparison.OrdinalIgnoreCase)&&!path.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase);
    string Owned(string path)
    {
        var full=Path.GetFullPath(path);var relative=Path.GetRelativePath(root,full);
        if(relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal)||Path.IsPathRooted(relative))throw new InvalidOperationException("Journal mutation escaped its owner directory.");
        return full;
    }
    long Reserve(string path,long length)=>Pending(path)?Math.Max(0,2*maximumFrame-length):0;
    static long Length(string path){try{return new FileInfo(path).Length;}catch(FileNotFoundException){return 0;}catch(DirectoryNotFoundException){return 0;}}
    long KnownLength(string path)=>files.TryGetValue(path,out var length)?length:Length(path);
    void Refresh(string path,long before)
    {
        var length=Length(path);used=checked(used+length-before);
        if(Pending(path))
        {
            if(files.Remove(path,out var prior))reserved-=Reserve(path,prior);
            if(File.Exists(path)){files[path]=length;reserved=checked(reserved+Reserve(path,length));}
        }
        else if(settlementFiles.Values.Any(value=>value.Path==path))files[path]=length;
    }
    void RefreshAll()
    {
        // Completed history contributes only its streamed byte total, not one
        // retained path/object per historical receipt. Hot metadata stays bounded
        // by pending ownership, independently of history cardinality.
        files.Clear();used=0;reserved=0;
        foreach(var entry in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
        {
            var path=Path.GetFullPath(entry);var length=Length(path);used=checked(used+length);
            if(Pending(path)){files[path]=length;reserved=checked(reserved+Reserve(path,length));}
        }
    }
    void RequireCapacity(long extra)
    {
        if(failure is{} error)throw new IOException("Journal capacity accounting is unavailable; reopen authentic evidence.",error);
        var held=checked(reserved+AuxiliaryReserve());
        if(used>maximumBytes||held>maximumBytes-used||extra>maximumBytes-used-held)throw new IOException("Journal retained history and settlement headroom exceed capacity; source must remain retained.");
    }
    /// <summary>Definite refusal occurs before IO. Pending slots retain rewrite/growth headroom until the exact final disposition moves them.</summary>
    public void ReserveSettlement(string owner,string path,int length)
    {
        lock(gate)
        {
            owner=Owned(owner);path=Owned(path);if(!Pending(owner)||!files.ContainsKey(owner))throw new InvalidOperationException("Settlement reserve needs an owned pending frame.");
            var extra=settlementFiles.Values.Any(value=>value.Path==path)?0:Math.Max(0,length-KnownLength(path));RequireCapacity(extra);files[path]=Length(path);settlementFiles[owner]=(path,length);
        }
    }
    public void Write(string path,int length,Action persist,string? settlementPath=null,int settlementLength=0,string? settlementOwner=null)
    {
        ArgumentNullException.ThrowIfNull(persist);if(length<1||length>maximumFrame)throw new InvalidDataException("Journal frame exceeds its reserved bound.");
        lock(gate)
        {
            path=Owned(path);var isPending=Pending(path);
            if(settlementPath is not null)settlementPath=Owned(settlementPath);
            if(settlementOwner is not null)settlementOwner=Owned(settlementOwner);
            var ownedSettlement=settlementOwner is not null&&files.ContainsKey(settlementOwner)&&settlementFiles.TryGetValue(settlementOwner,out var planned)&&planned.Path==path&&length<=planned.Length;
            var extra=isPending?(files.ContainsKey(path)?0:2*maximumFrame):ownedSettlement?0:length;
            if(settlementPath is not null&&!settlementFiles.Values.Any(value=>value.Path==settlementPath))extra+=Math.Max(0,settlementLength-KnownLength(settlementPath));
            RequireCapacity(extra);
            var before=Length(path);var temporaryBefore=Length(path+".tmp");
            try{persist();}
            finally
            {
                try{Refresh(path,before);Refresh(path+".tmp",temporaryBefore);if(isPending&&files.ContainsKey(path)&&settlementPath is not null){files[settlementPath]=Length(settlementPath);settlementFiles[path]=(settlementPath,settlementLength);}}
                catch(Exception error){failure=error;throw;}
            }
        }
    }
    /// <summary>Records only the caller's existing durable owner-disposition move; it does not invent a retention horizon.</summary>
    public void Move(string source,string target,Action persist)
    {
        ArgumentNullException.ThrowIfNull(persist);
        lock(gate)
        {
            source=Owned(source);target=Owned(target);RequireCapacity(0);
            var sourceBefore=Length(source);var targetBefore=Length(target);
            try{persist();}
            finally
            {
                try{Refresh(source,sourceBefore);Refresh(target,targetBefore);if(!files.ContainsKey(source)&&settlementFiles.Remove(source,out var auxiliary)&&!settlementFiles.Values.Any(value=>value.Path==auxiliary.Path))files.Remove(auxiliary.Path);}
                catch(Exception error){failure=error;throw;}
            }
        }
    }
}
