using System.Reflection;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests.ScheduledTask;

public sealed class ScheduledTaskScenarioCoverageTests
{
    [Theory]
    [InlineData(typeof(ScheduledTaskCommandActor))]
    [InlineData(typeof(ScheduledTaskRunCommandActor))]
    [InlineData(typeof(ScheduledTaskCatalogCommandActor))]
    public void Every_mapped_command_has_happy_path_and_edge_case_BDD_scenarios(Type actor)
    {
        var commands = (IReadOnlyCollection<Type>)actor.GetProperty("SupportedCommandTypes")!.GetValue(null)!;
        var scenarios = typeof(ScheduledTaskScenarioCoverageTests).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ScheduledTaskScenarioCoverageTests).Namespace)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Select(m => (Method: m, Traits: m.GetCustomAttributesData().Where(a => a.AttributeType == typeof(TraitAttribute))
                .Select(a => (Name: (string)a.ConstructorArguments[0].Value!, Value: (string)a.ConstructorArguments[1].Value!)).ToArray())).ToArray();
        foreach (var command in commands)
        foreach (var category in new[] { "HappyPath", "EdgeCase" })
        {
            Assert.Contains(scenarios, s => s.Traits.Any(t => t.Name == "Command" && t.Value == command.Name)
                && s.Traits.Any(t => t.Name == "Scenario" && t.Value == category)
                && (s.Method.GetCustomAttribute<FactAttribute>() is not null));
        }
    }
}
