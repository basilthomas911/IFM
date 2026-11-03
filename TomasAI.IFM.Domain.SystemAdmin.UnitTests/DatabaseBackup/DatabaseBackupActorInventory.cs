using System.Reflection;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;
internal static class DatabaseBackupActorInventory
{
    internal static Type[] Actors(string kind) => typeof(DatabaseBackupCommandActor).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && t.Namespace?.StartsWith("TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.", StringComparison.Ordinal) == true
            && t.Name.EndsWith(kind + "Actor", StringComparison.Ordinal)).ToArray();
    internal static Type[] Types(string kind, string property) => Actors(kind).SelectMany(t =>
        (IReadOnlyCollection<Type>)t.GetProperty(property, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!).Distinct().ToArray();
    internal static string[] Verbs(string kind) => Actors(kind).SelectMany(t =>
        (IReadOnlyCollection<string>)t.GetProperty("SupportedVerbs", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!).Distinct(StringComparer.Ordinal).ToArray();
    internal static Type[] CommandTypes => Types("Command", "SupportedCommandTypes");
    internal static Type[] EventTypes => Types("Event", "SupportedServiceEventTypes");
    internal static Type[] QueryTypes => Types("Query", "SupportedQueryTypes");
    internal static string[] CommandVerbs => Verbs("Command");
    internal static string[] EventVerbs => Verbs("Event");
    internal static string[] QueryVerbs => Verbs("Query");
}
