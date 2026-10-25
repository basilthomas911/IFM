using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.UI.Net.Models;

internal static class SidebarFixtures
{
    public static void Export(string directory)
    {
        var times=new[]{new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc),new DateTime(2026,11,1,5,30,0,DateTimeKind.Utc),new DateTime(2026,11,1,6,30,0,DateTimeKind.Utc)};
        // The unchanged SidebarSession uses this exact display expression.
        FixtureFiles.WriteAllText(Path.Combine(directory,"sidebar-time-display.json"),JsonSerializer.Serialize(times.Select(time=>new{time,expected=EasternTime.FromUtc(time).ToString("t",CultureInfo.CurrentCulture)})));
    }
}
