using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace TomasAI.IFM.Shared.Reference.ViewModels
{
    [MessagePackObject(AllowPrivate = true)]
    public class FuturesOptionStrikePriceReadModel
    {
        [Key(0)]
        public int Minimum { get; set; }
        
        [Key(1)]
        public int Maximum { get; set; }
        
        [Key(2)]
        public int Increment { get; set; }
    }
}
