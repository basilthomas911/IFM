using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Domain.BDDTests
{
    public class BDDTestAttribute : Attribute
    {

        public BDDTestAttribute()
        {

        }

        public string Given { get; set; } = string.Empty;
        public string When { get; set; } = string.Empty;
        public string Then { get; set; } = string.Empty;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"given {this.Given}");
            sb.AppendLine($"when {this.When}");
            sb.AppendLine($"then {this.Then}");
            return $"{sb}";
        }
    }
}
