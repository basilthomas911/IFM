using System.Reflection;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

/// <summary>Checks actual mapped ingress for malformed serialized command payloads.</summary>
public sealed class CommandActorValidationTests
{
    public static IEnumerable<object[]> MalformedCommands()
    {
        var names = new HashSet<string>(["DatabaseBackupCommandActor"]);
        foreach (var actor in typeof(TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Actor.DatabaseBackupCommandActor).Assembly.GetTypes().Where(type => names.Contains(type.Name)))
        {
            var map = Map(actor);
            foreach (var commandType in map.Keys) yield return [actor, commandType];
        }
    }

    [Theory]
    [MemberData(nameof(MalformedCommands))]
    public void Missing_identity_and_payload_are_aggregated_without_throwing(Type actor, Type commandType)
    {
        var command = (ICommand)Activator.CreateInstance(commandType)!;
        var errors = Map(actor)[commandType](command);
        Assert.True(errors.Count >= 2, $"{commandType.Name} must report independent identity/payload failures.");
        Assert.Contains(errors, error => error.ErrorMessage.Contains("CommandId", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(MalformedCommands))]
    public void Null_serialized_reference_fields_do_not_bypass_aggregate_validation(Type actor, Type commandType)
    {
        foreach (var property in commandType.GetProperties().Where(property =>
            property.CanWrite && !property.PropertyType.IsValueType && property.PropertyType != typeof(string) &&
            property.GetCustomAttribute<MessagePack.KeyAttribute>() is not null))
        {
            var command = (ICommand)Activator.CreateInstance(commandType)!;
            property.SetValue(command, null);
            var errors = Map(actor)[commandType](command);
            Assert.True(errors.Count >= 2, $"{commandType.Name}.{property.Name} must remain an aggregate validation failure.");
        }
    }

    private static IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> Map(Type actor)
        => (IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>>)actor
            .GetField("_validationMap", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
