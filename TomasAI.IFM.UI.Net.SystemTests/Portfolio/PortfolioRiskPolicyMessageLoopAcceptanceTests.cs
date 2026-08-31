using System.Reflection;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Portfolio;
using WinButton = System.Windows.Forms.Button;
using WinTextBox = System.Windows.Forms.TextBox;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PortfolioRiskPolicyMessageLoopCollection
{
    public const string Name = "Portfolio Risk Policy message-loop acceptance";
}

[Collection(PortfolioRiskPolicyMessageLoopCollection.Name)]
public sealed class PortfolioRiskPolicyMessageLoopAcceptanceTests
{
    [Fact]
    [Trait("Gate", "PF-27")]
    [Trait("Category", "PortfolioInteractive")]
    public async Task Rendered_operator_journeys_save_reject_dirty_close_and_enforce_read_only_access()
    {
        var identities = Substitute.For<IPortfolioIdentityApi>();
        identities.AllocatePolicyIdAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioBusinessIdAllocation>(new()
            {
                Kind = PortfolioBusinessIdentityKind.Policy,
                Value = 9101,
                CorrelationId = Guid.NewGuid(),
            }));
        var commands = Substitute.For<IPortfolioFinancialPolicyCommandApi>();
        commands.CreatePolicyAsync(
                Arg.Any<PortfolioFinancialPolicyReadModel>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var rejectedDirtyCloses = 0;

        await using (var host = await FormHost.StartAsync(() => new PortfolioRiskPolicyForm(
                         Portfolio(), EmptyPolicyQueries(), identities, commands, ReferenceCatalog(), true,
                         () => { Interlocked.Increment(ref rejectedDirtyCloses); return false; })))
        {
            using var automation = new UIA3Automation();
            var rendered = automation.FromHandle(host.Handle).AsWindow();
            rendered.Title.Should().Be("Portfolio Risk Policy");
            rendered.IsEnabled.Should().BeTrue();

            await host.InvokeAsync(form => Field<WinButton>(form, "_newPolicy").PerformClick());
            await WaitUntilAsync(() => identities.ReceivedCalls().Count(call =>
                call.GetMethodInfo().Name == nameof(IPortfolioIdentityApi.AllocatePolicyIdAsync)) == 1);
            await host.InvokeAsync(form => Field<WinTextBox>(form, "_name").Text = "Operator limits");
            await host.InvokeAsync(form => Field<WinButton>(form, "_save").PerformClick());
            await WaitUntilAsync(() => commands.ReceivedCalls().Any(call =>
                call.GetMethodInfo().Name == nameof(IPortfolioFinancialPolicyCommandApi.CreatePolicyAsync)));

            await commands.Received(1).CreatePolicyAsync(
                Arg.Is<PortfolioFinancialPolicyReadModel>(policy =>
                    policy.PortfolioId == 7001 && policy.PolicyId == 9101 && policy.Name == "Operator limits" &&
                    policy.TradeFamilyLimits.Length == 3),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>());

            await host.InvokeAsync(form => Field<WinButton>(form, "_newPolicy").PerformClick());
            await WaitUntilAsync(() => identities.ReceivedCalls().Count(call =>
                call.GetMethodInfo().Name == nameof(IPortfolioIdentityApi.AllocatePolicyIdAsync)) == 2);
            await host.InvokeAsync(form => Field<WinTextBox>(form, "_name").Text = "Unsaved operator change");
            await host.InvokeAsync(form => form.Close());
            await WaitUntilAsync(() => Volatile.Read(ref rejectedDirtyCloses) == 1);
            host.Form.IsDisposed.Should().BeFalse("the operator rejected discarding unsaved changes");
            host.Thread.IsAlive.Should().BeTrue("the rendered message loop remains active after rejected close");
        }

        await using (var host = await FormHost.StartAsync(() => new PortfolioRiskPolicyForm(
                         Portfolio(), EmptyPolicyQueries(), Substitute.For<IPortfolioIdentityApi>(),
                         Substitute.For<IPortfolioFinancialPolicyCommandApi>(), ReferenceCatalog(), false)))
        {
            using var automation = new UIA3Automation();
            automation.FromHandle(host.Handle).AsWindow().Title.Should().Be("Portfolio Risk Policy");
            var mutationControls = new[] { "_newPolicy", "_newVersion", "_save", "_cancel", "_activate", "_retire", "_delete" };
            var enabled = await host.InvokeAsync(form => mutationControls.Select(name => Field<WinButton>(form, name).Enabled).ToArray());
            enabled.Should().OnlyContain(value => !value);
            (await host.InvokeAsync(form => Field<WinTextBox>(form, "_name").ReadOnly)).Should().BeTrue();
        }
    }

    static IPortfolioQueryApi EmptyPolicyQueries()
    {
        var queries = Substitute.For<IPortfolioQueryApi>();
        queries.GetPoliciesAsync(7001, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<PortfolioPage<PortfolioFinancialPolicyReadModel>>(new() { Items = [], PageSize = 200 }));
        return queries;
    }

    static IReferenceQueryApi ReferenceCatalog()
    {
        var references = Substitute.For<IReferenceQueryApi>();
        references.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<TradeStrategyFamilyReadModel[]>(
            [
                Family(1, "FUTURES", "Futures"),
                Family(2, "VERTICAL_SPREAD", "Vertical Spread"),
                Family(3, "IRON_CONDOR", "Iron Condor"),
            ]));
        return references;
    }

    static TradeStrategyFamilyReadModel Family(int id, string key, string name) => new()
    {
        TradeStrategyFamilyId = id, DefinitionVersion = 1, SystemKey = key, Name = name,
        State = TradeStrategyFamilyState.Active, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "PF-27 acceptance",
    };

    static PortfolioReadModel Portfolio() => new()
    {
        PortfolioId = 7001, PortfolioVersion = 2, Name = "Core", BaseCurrency = "USD",
        OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        CreatedOnUtc = DateTime.UtcNow.AddMinutes(-1), CreatedBy = "PF-27 acceptance",
    };

    static T Field<T>(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) is T value
            ? value
            : throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}.");

    static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
            await Task.Delay(50, timeout.Token);
    }

    sealed class FormHost : IAsyncDisposable
    {
        FormHost(PortfolioRiskPolicyForm form, Thread thread, IntPtr handle)
        {
            Form = form;
            Thread = thread;
            Handle = handle;
        }

        public PortfolioRiskPolicyForm Form { get; }
        public Thread Thread { get; }
        public IntPtr Handle { get; }

        public static async Task<FormHost> StartAsync(Func<PortfolioRiskPolicyForm> create)
        {
            var ready = new TaskCompletionSource<(PortfolioRiskPolicyForm Form, IntPtr Handle)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    var form = create();
                    form.Shown += (_, _) => ready.TrySetResult((form, form.Handle));
                    System.Windows.Forms.Application.Run(form);
                }
                catch (Exception exception)
                {
                    ready.TrySetException(exception);
                }
            }) { IsBackground = true, Name = "PF-27 Risk Policy acceptance" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            var result = await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return new FormHost(result.Form, thread, result.Handle);
        }

        public Task InvokeAsync(Action<PortfolioRiskPolicyForm> action) => InvokeAsync(form => { action(form); return true; });

        public Task<TResult> InvokeAsync<TResult>(Func<PortfolioRiskPolicyForm, TResult> action)
        {
            var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Form.BeginInvoke(() =>
            {
                try { completion.SetResult(action(Form)); }
                catch (Exception exception) { completion.SetException(exception); }
            });
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public async ValueTask DisposeAsync()
        {
            if (!Form.IsDisposed && Form.IsHandleCreated)
            {
                await InvokeAsync(form =>
                {
                    typeof(PortfolioRiskPolicyForm).GetMethod("CancelEdit", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(form, null);
                    form.Close();
                });
            }
            Thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue("the PF-27 WinForms message loop must close cleanly");
        }
    }
}
