using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public enum DeviceLocationType
    {
        Local,
        Remote
    }

    public static class DeviceLocationTypeExtensions
    {
        public static string ToStringFast(this DeviceLocationType value) => value switch
        {
            DeviceLocationType.Local => nameof(DeviceLocationType.Local),
            DeviceLocationType.Remote => nameof(DeviceLocationType.Remote),
            _ => value.ToString()
        };
    }
}
