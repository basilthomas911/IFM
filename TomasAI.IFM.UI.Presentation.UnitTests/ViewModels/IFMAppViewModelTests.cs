using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Presentation.UnitTests.ViewModels;

public class IFMAppViewModelTests
{
    [Fact]
    public void StatusLogState_IsNewestFirstAndBounded()
    {
        var viewModel = CreateSubject();

        for (var index = 0; index < 505; index++)
        {
            viewModel.AppendStatusLog(new StatusConsoleLogReadModel(
                new DateTime(2026, 8, 11).AddSeconds(index),
                0,
                LogSourceType.IFMApp,
                $"message-{index}"));
        }

        viewModel.StatusLogs.Should().HaveCount(500);
        viewModel.StatusLogs[0].Message.Should().Be("message-504");
        viewModel.StatusLogs[^1].Message.Should().Be("message-5");
        viewModel.LatestStatusLog.Should().BeSameAs(viewModel.StatusLogs[0]);
        viewModel.StatusLine.Should().Be("message-504");
    }

    [Fact]
    public void RepeatedErrors_AreDistinctObservableNotifications()
    {
        var viewModel = CreateSubject();
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changes.Add(eventArgs.PropertyName);

        viewModel.PublishError(41, "backend unavailable", "Startup Error");
        var first = viewModel.LastError;
        viewModel.PublishError(41, "backend unavailable", "Startup Error");

        viewModel.LastError!.Sequence.Should().BeGreaterThan(first!.Sequence);
        changes.Count(name => name == nameof(IFMAppViewModel.LastError)).Should().Be(2);
    }

    [Fact]
    public void ShellSurface_IsObservableAndDeclaresNoDelegateCallbacks()
    {
        var viewModel = CreateSubject();

        viewModel.IsMenuEnabled.Should().BeFalse();
        viewModel.IsCloseRequested.Should().BeFalse();
        viewModel.StartupOperation.Should().NotBeNull();
        viewModel.ShutdownOperation.Should().NotBeNull();
        typeof(IFMAppViewModel)
            .GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
            .Should().BeEmpty();
    }

    static IFMAppViewModel CreateSubject()
    {
        var commandResponseConsumer = Substitute.For<ICommandResponseUIEventConsumer>();
        var eventModel = new EventModel(commandResponseConsumer);
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.GetModel<EventModel>().Returns(eventModel);
        return new IFMAppViewModel(
            appRoot,
            new Version(1, 2, 3),
            "Test",
            Substitute.For<IIFMAppLiveViewAdapter>());
    }
}
