using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;
using TomasAI.IFM.UI.Net.Views.SystemAdmin;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.SystemTests;

[Trait("Category", "Gate9System")]
public sealed class DatabaseBackupDashboardSmokeTests
{
    [Fact]
    public async Task Dashboard_starts_renders_disposable_backend_state_and_remains_responsive()
    {
        var queryApi = CreateDisposableQueryApi();
        var eventConsumer = Substitute.For<ISystemAdminUIEventConsumer>();
        var service = new DatabaseBackupService(
            Substitute.For<IDatabaseBackupCommandApi>(), queryApi, eventConsumer);
        var viewModel = new DatabaseBackupViewModel(service);
        var ready = new TaskCompletionSource<(Form Form, BackupDatabasesView View, IntPtr Handle)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? uiFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form
                {
                    Text = $"IFM Database Backup Gate 9 {Guid.NewGuid():N}",
                    Width = 1100,
                    Height = 520
                };
                var view = new BackupDatabasesView(viewModel) { Dock = DockStyle.Fill };
                form.Controls.Add(view);
                form.Shown += (_, _) => ready.TrySetResult((form, view, form.Handle));
                view.Open();
                System.Windows.Forms.Application.Run(form);
            }
            catch (Exception exception)
            {
                uiFailure = exception;
                ready.TrySetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var (form, view, formHandle) = await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(formHandle).AsWindow();
            window.Should().NotBeNull();
            window!.Title.Should().Be(form.Text);
            window.IsEnabled.Should().BeTrue();
            window.Focus();

            var controlState = await ReadControlStateAsync(form, view);
            controlState.ProtectionSets.Should().Contain("core");
            controlState.RequestButtonText.Should().Be("Request Backup");
            controlState.RequestButtonEnabled.Should().BeFalse(
                "a backup request requires an explicitly checked protection set");
            controlState.LocalSourceSelected.Should().BeTrue();

            await CheckProtectionSetAsync(form, view, "core");
            controlState = await ReadControlStateAsync(form, view);
            controlState.RequestButtonEnabled.Should().BeTrue();
        }
        finally
        {
            form.BeginInvoke(form.Close);
            thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();
        }

        uiFailure.Should().BeNull();
        await queryApi.Received().GetProtectionSetsAsync(
            Arg.Any<GetDatabaseProtectionSetsQuery>(), Arg.Any<CancellationToken>());
    }

    static Task<DashboardControlState> ReadControlStateAsync(Form form, BackupDatabasesView view)
    {
        var completion = new TaskCompletionSource<DashboardControlState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        form.BeginInvoke(() =>
        {
            var list = FindControl<CheckedListBox>(view, "clbDatabases");
            var button = FindControl<System.Windows.Forms.Button>(view, "btnRun");
            var localSource = FindControl<System.Windows.Forms.RadioButton>(view, "radDiffBackup");
            completion.SetResult(new DashboardControlState(
                list.Items.Cast<object>().Select(item => item.ToString() ?? string.Empty).ToArray(),
                button.Text,
                button.Enabled,
                localSource.Checked));
        });
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    static Task CheckProtectionSetAsync(Form form, BackupDatabasesView view, string protectionSet)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        form.BeginInvoke(() =>
        {
            var list = FindControl<CheckedListBox>(view, "clbDatabases");
            var index = list.Items.Cast<object>()
                .Select((item, itemIndex) => (Value: item.ToString(), Index: itemIndex))
                .Single(item => string.Equals(item.Value, protectionSet, StringComparison.Ordinal))
                .Index;
            list.SetItemChecked(index, true);
            completion.SetResult();
        });
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    static TControl FindControl<TControl>(Control parent, string name) where TControl : Control
        => parent.Controls.Find(name, true).OfType<TControl>().Single();

    static IDatabaseBackupQueryApi CreateDisposableQueryApi()
    {
        var queryApi = Substitute.For<IDatabaseBackupQueryApi>();
        queryApi.GetProtectionSetsAsync(Arg.Any<GetDatabaseProtectionSetsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>>(
                new ServiceOk<DatabaseProtectionSetReadModel[]>(
                [new DatabaseProtectionSetReadModel
                {
                    ProtectionSetId = new DatabaseProtectionSetId("core"),
                    Source = BackupSource.LocalWorkstation,
                    Engines = [DatabaseEngine.PostgreSql, DatabaseEngine.ScyllaDb],
                    Enabled = true,
                    PolicyRevision = 1
                }])));
        queryApi.ListBackupOperationsAsync(Arg.Any<ListDatabaseBackupOperationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseBackupOperationReadModel[]>>(
                new ServiceOk<DatabaseBackupOperationReadModel[]>([])));
        queryApi.GetLatestVerifiedBackupAsync(
                Arg.Any<GetLatestVerifiedDatabaseBackupQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseRestorePointReadModel>>(
                new ServiceFailed<DatabaseRestorePointReadModel>(404, "No verified restore point.")));
        queryApi.GetLatestRestoreTestedBackupAsync(
                Arg.Any<GetLatestRestoreTestedDatabaseBackupQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseRestorePointReadModel>>(
                new ServiceFailed<DatabaseRestorePointReadModel>(404, "No restore-tested point.")));
        return queryApi;
    }

    sealed record DashboardControlState(
        string[] ProtectionSets,
        string RequestButtonText,
        bool RequestButtonEnabled,
        bool LocalSourceSelected);
}
