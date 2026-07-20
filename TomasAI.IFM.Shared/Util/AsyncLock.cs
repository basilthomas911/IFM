using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace TomasAI.IFM.Shared.Util
{
    public class AsyncLock
    {
        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1,1);

        //public async Task RunAsync(Action lockAction)
    }
}
