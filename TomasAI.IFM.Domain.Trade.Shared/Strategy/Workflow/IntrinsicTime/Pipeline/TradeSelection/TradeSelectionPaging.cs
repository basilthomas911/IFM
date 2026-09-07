using MessagePack;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
public static class TradeSelectionPaging
{
    [MessagePackObject] public sealed record Token([property:Key(0)] short Schema,[property:Key(1)] int PortfolioId,[property:Key(2)] int FundId,[property:Key(3)] DateOnly ValueDate,[property:Key(4)] int PageSize,[property:Key(5)] byte[] State);
    public static string? Encode(int portfolio,int fund,DateOnly date,int size,byte[]? state)=>state is null?null:Convert.ToBase64String(MessagePackSerializer.Serialize(new Token(1,portfolio,fund,date,size,state)));
    public static byte[]? Decode(int portfolio,int fund,DateOnly date,int size,string? token)
    {
        if(token is null)return null;
        TradeSelectionContracts.Require(token.Length<=32768,"TS.QUERY.PAGING","Paging token too large.");
        try
        {
            var p=MessagePackSerializer.Deserialize<Token>(Convert.FromBase64String(token));
            TradeSelectionContracts.Require(p.Schema==1 && p.PortfolioId==portfolio && p.FundId==fund && p.ValueDate==date && p.PageSize==size && p.State.Length>0,"TS.QUERY.PAGING","Paging token does not match the query scope.");return p.State;
        }
        catch(Exception ex) when(ex is FormatException or MessagePackSerializationException){throw new ArgumentException("Invalid selection paging token.",ex);}
    }
}
