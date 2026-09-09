using TomasAI.IFM.Framework.Serialization;
using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
public static class RiskPaging
{
    static void Require(bool condition,string reason) { if(!condition) throw new ArgumentException(reason); }
    [MessagePackObject] public sealed record Token([property:Key(0)] short Schema,[property:Key(1)] int PortfolioId,[property:Key(2)] int FundId,[property:Key(3)] DateOnly ValueDate,[property:Key(4)] int PageSize,[property:Key(5)] byte[] State);
    public static string? Encode(int portfolio,int fund,DateOnly date,int size,byte[]? state)=>state is null?null:Convert.ToBase64String(MessagePackBinarySerializer.Shared.Serialize(new Token(2,portfolio,fund,date,size,state)));
    public static byte[]? Decode(int portfolio,int fund,DateOnly date,int size,string? token)
    {
        if(token is null)return null;
        Require(token.Length<=32768,"RM.QUERY.PAGING");
        try
        {
            var p=MessagePackBinarySerializer.Shared.Deserialize<Token>(Convert.FromBase64String(token));
            Require(p.Schema==2 && p.PortfolioId==portfolio && p.FundId==fund && p.ValueDate==date && p.PageSize==size && p.State.Length>0,"RM.QUERY.PAGING");return p.State;
        }
        catch(Exception ex) when(ex is FormatException or MessagePackSerializationException){throw new ArgumentException("Invalid Risk paging token.",ex);}
    }
}
