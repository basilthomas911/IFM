using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using TomasAI.IFM.UI.Rust.Bridge.Net;
using Xunit;
public unsafe class AbiTests
{
 [Theory] [InlineData(1000)] [InlineData(10000)] [InlineData(100000)]
 public void Managed_grid_has_all_rows_and_exact_values(int count){var rows=VirtualGridDataset.Create(count);Assert.Equal(count,rows.Length);Assert.Equal(1,rows[0].Sequence);Assert.Equal(count,rows[^1].Id);Assert.Equal("-99.90",rows[0].DailyPnl);Assert.Equal("0.01",rows[0].ForwardLossRatio);Assert.Equal(rows.Length,rows.Select(r=>r.Id).Distinct().Count());}
 [Fact] public void Version_layout_buffer_capacity_and_stale_handles_are_enforced()
 {
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;
  ApiV1 api;Assert.Equal(40,sizeof(ApiV1));Assert.Equal(-1,get(9,&api,40));Assert.Equal(-1,get(1,&api,8));Assert.Equal(0,get(1,&api,40));
  ulong handle;Assert.Equal(0,api.Create(&handle));
  try{
   int required;Assert.Equal(-3,api.Copy(999999,1,null,0,&required));Assert.Equal(-1,api.Copy(handle,9,null,0,&required));
   Assert.Equal(1,api.Copy(handle,1,null,0,&required));Assert.True(required>0);
   var buffer=new byte[required];fixed(byte* p=buffer)Assert.Equal(0,api.Copy(handle,1,p,buffer.Length,&required));
   using var catalog=JsonDocument.Parse(buffer);Assert.Equal(1,catalog.RootElement.GetProperty("version").GetInt32());
   Assert.Equal(-1,api.Action(handle,99));
  }finally{Assert.Equal(0,api.Destroy(handle));}
  Assert.Equal(-3,api.Destroy(handle));
 }
 [Theory][InlineData("strategy-catalog")][InlineData("parameter-sets")]
 public void Version_two_routes_each_typed_editor_view_through_the_same_gateway(string view)
 {
  delegate* unmanaged[Cdecl]<uint,ApiV1*,uint,int> get=&BridgeBootstrap.GetApi;
  ApiV2 api;Assert.Equal(0,get(2,(ApiV1*)&api,(uint)sizeof(ApiV2)));ulong handle;Assert.Equal(0,api.Base.Create(&handle));
  try{var bytes=JsonSerializer.SerializeToUtf8Bytes(new{schema=1,requestId=Guid.NewGuid(),view,intent="load",expectedGeneration=0,originalId=(string?)null,fields=new Dictionary<string,string>()});fixed(byte* p=bytes)Assert.Equal(0,api.Submit(handle,p,bytes.Length));}
  finally{Assert.Equal(0,api.Base.Destroy(handle));}
 }
}
