using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketData
{
    public class TickFeedResult : ITickFeedResult
    {
        public string ErrorMessage { get; }
        public bool Succeeded { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="succeeded"></param>
        public TickFeedResult(string errorMessage, bool succeeded)
        {
            this.ErrorMessage = errorMessage;
            this.Succeeded = succeeded;
        }

    }
}
