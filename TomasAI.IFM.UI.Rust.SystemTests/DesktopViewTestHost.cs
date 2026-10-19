using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Owns a real STA WinForms message loop for cross-thread UI Automation parity tests.</summary>
sealed class DesktopViewTestHost : IDisposable
{
 readonly TaskCompletionSource<IntPtr> ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
 readonly TaskCompletionSource closed=new(TaskCreationOptions.RunContinuationsAsynchronously);
 System.Windows.Forms.Form? form;
 Func<Task>? close;
 public IntPtr Handle => ready.Task.GetAwaiter().GetResult();
 public IntPtr FindFormHandle(string title) => form is null || form.IsDisposed ? IntPtr.Zero
  : (IntPtr)form.Invoke(new Func<IntPtr>(() => System.Windows.Forms.Application.OpenForms.Cast<System.Windows.Forms.Form>()
      .FirstOrDefault(candidate => candidate.Text == title)?.Handle ?? IntPtr.Zero));
 public DesktopViewTestHost(Func<RustClientServiceScope,(System.Windows.Forms.Form Form,Func<Task> Close)> create)
 {
  var thread=new Thread(()=>{
   RustClientServiceScope? scope=null;
   try{
    scope=new("nats://localhost:4222");scope.StartAsync().AsTask().GetAwaiter().GetResult();
    var result=create(scope);form=result.Form;close=result.Close;
    form.Shown+=(_,_)=>ready.TrySetResult(form.Handle);
    System.Windows.Forms.Application.Run(form);
   }catch(Exception e){ready.TrySetException(e);closed.TrySetException(e);}
   finally{SynchronizationContext.SetSynchronizationContext(null);try{form?.Dispose();scope?.DisposeAsync().AsTask().GetAwaiter().GetResult();closed.TrySetResult();}catch(Exception e){closed.TrySetException(e);}}
  }){IsBackground=true,Name="WinForms parity STA host"};thread.SetApartmentState(ApartmentState.STA);thread.Start();ready.Task.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
 }
 public void Dispose()
 {
  if(form is not null&&!form.IsDisposed)form.BeginInvoke(new Action(async()=>{try{if(close is not null)await close();form.Close();}catch(Exception e){closed.TrySetException(e);form.Close();}}));
  closed.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
 }
}
