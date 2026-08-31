using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.ViewModels.Portfolio;

public enum PortfolioUiState { Idle, Loading, Ready, Empty, PendingProjection, ValidationError, Conflict, Timeout, Unavailable, Unauthorized }

/// <summary>Framework-neutral Portfolio administration state; every operation crosses a typed NATS API.</summary>
public sealed class PortfolioAdministrationViewModel(
    IPortfolioQueryApi queries,
    IPortfolioCommandApi commands,
    IPortfolioFundCommandApi fundCommands,
    IPortfolioIdentityApi identities,
    bool canMutate) : ObservableObject
{
    readonly IPortfolioQueryApi _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    readonly IPortfolioCommandApi _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    readonly IPortfolioFundCommandApi _fundCommands = fundCommands ?? throw new ArgumentNullException(nameof(fundCommands));
    readonly IPortfolioIdentityApi _identities = identities ?? throw new ArgumentNullException(nameof(identities));
    long _loadGeneration;
    PortfolioUiState _state;
    string _message = string.Empty;
    PortfolioReadModel[] _portfolios = [];
    PortfolioReadModel? _selectedPortfolio;
    FundMandateReadModel[] _funds = [];
    FundMandateReadModel? _selectedFund;
    FundAllocationReadModel? _allocation;
    FundRiskEnvelopeReadModel? _riskEnvelope;
    FundTradeTemplateAssignmentReadModel[] _assignments = [];
    long _portfolioRevision;
    long _fundRevision;

    public bool CanMutate { get; } = canMutate;
    public PortfolioUiState State { get => _state; private set => SetProperty(ref _state, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public PortfolioReadModel[] Portfolios { get => _portfolios; private set => SetProperty(ref _portfolios, value); }
    public PortfolioReadModel? SelectedPortfolio { get => _selectedPortfolio; private set => SetProperty(ref _selectedPortfolio, value); }
    public FundMandateReadModel[] Funds { get => _funds; private set => SetProperty(ref _funds, value); }
    public FundMandateReadModel? SelectedFund { get => _selectedFund; private set => SetProperty(ref _selectedFund, value); }
    public FundAllocationReadModel? Allocation { get => _allocation; private set => SetProperty(ref _allocation, value); }
    public FundRiskEnvelopeReadModel? RiskEnvelope { get => _riskEnvelope; private set => SetProperty(ref _riskEnvelope, value); }
    public FundTradeTemplateAssignmentReadModel[] Assignments { get => _assignments; private set => SetProperty(ref _assignments, value); }
    public long PortfolioRevision { get => _portfolioRevision; private set => SetProperty(ref _portfolioRevision, value); }
    public long FundRevision { get => _fundRevision; private set => SetProperty(ref _fundRevision, value); }

    public async Task LoadAsync(PortfolioOperatingState state, CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        State = PortfolioUiState.Loading;
        try
        {
            var result = await _queries.GetPortfoliosAsync(state, 100, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (generation != Volatile.Read(ref _loadGeneration)) return;
            if (!result.Success) { MapError(result.ErrorCode, result.ErrorMessage); return; }
            Portfolios = result.Value?.Items ?? [];
            State = Portfolios.Length == 0 ? PortfolioUiState.Empty : PortfolioUiState.Ready;
            Message = string.Empty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (generation == Volatile.Read(ref _loadGeneration)) State = PortfolioUiState.Idle;
            throw;
        }
        catch (TimeoutException ex) { if (generation == Volatile.Read(ref _loadGeneration)) { State = PortfolioUiState.Timeout; Message = ex.Message; } }
        catch (Exception ex) { if (generation == Volatile.Read(ref _loadGeneration)) { State = PortfolioUiState.Unavailable; Message = ex.Message; } }
    }

    public async Task SelectPortfolioAsync(PortfolioReadModel portfolio, CancellationToken cancellationToken = default)
    {
        SelectedPortfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        SelectedFund = null; Funds = []; PortfolioRevision = 0; FundRevision = 0; ClearConfiguration();
        var result = await _queries.GetFundsAsync(portfolio.PortfolioId, null, 100, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Success) { MapError(result.ErrorCode, result.ErrorMessage); return; }
        var revision = await _queries.GetPortfolioRevisionAsync(portfolio.PortfolioId, cancellationToken).ConfigureAwait(false);
        if (!revision.Success || revision.Value is not { Revision: > 0 } portfolioRevision) { MapError(revision.ErrorCode, revision.ErrorMessage); return; }
        PortfolioRevision = portfolioRevision.Revision;
        Funds = result.Value?.Items ?? [];
        State = PortfolioUiState.Ready;
        Message = Funds.Length == 0 ? "No Funds are configured for this Portfolio." : string.Empty;
    }

    public async Task SelectFundAsync(FundMandateReadModel fund, CancellationToken cancellationToken = default)
    {
        if (SelectedPortfolio is null || fund.PortfolioId != SelectedPortfolio.PortfolioId)
            throw new InvalidOperationException("Fund must belong to the selected Portfolio.");
        SelectedFund = fund;
        FundRevision = 0; ClearConfiguration();
        var portfolioRevision = await _queries.GetPortfolioRevisionAsync(fund.PortfolioId, cancellationToken).ConfigureAwait(false);
        if (!portfolioRevision.Success || portfolioRevision.Value is not { Revision: > 0 } currentPortfolio) { MapError(portfolioRevision.ErrorCode, portfolioRevision.ErrorMessage); return; }
        PortfolioRevision = currentPortfolio.Revision;
        var fundRevision = await _queries.GetFundRevisionAsync(fund.PortfolioId, fund.FundId, cancellationToken).ConfigureAwait(false);
        if (!fundRevision.Success || fundRevision.Value is not { Revision: > 0 } currentFund) { MapError(fundRevision.ErrorCode, fundRevision.ErrorMessage); return; }
        FundRevision = currentFund.Revision;
        var allocation = await _queries.GetFundAllocationAsync(fund.PortfolioId, fund.FundId, cancellationToken).ConfigureAwait(false);
        if (allocation.Success) Allocation = allocation.Value;
        var envelope = await _queries.GetFundRiskEnvelopeAsync(fund.PortfolioId, fund.FundId, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
        if (envelope.Success) RiskEnvelope = envelope.Value;
        var assignments = await _queries.GetAssignmentsAsync(fund.PortfolioId, fund.FundId, fund.FundMandateVersion, cancellationToken).ConfigureAwait(false);
        if (assignments.Success) Assignments = assignments.Value ?? [];
        State = PortfolioUiState.Ready;
    }

    public async Task<PortfolioIdentityAllocationModel> AllocatePortfolioIdAsync(CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return PortfolioIdentityAllocationModel.Failure(Message);
        var result = await _identities.AllocatePortfolioIdAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is not { Kind: PortfolioBusinessIdentityKind.Portfolio, Value: > 0 } value)
        {
            MapError(result.ErrorCode, result.ErrorMessage);
            return PortfolioIdentityAllocationModel.Failure(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Portfolio ID allocation failed." : result.ErrorMessage);
        }
        return PortfolioIdentityAllocationModel.Success(new PortfolioId(value.Value));
    }

    public async Task<int?> AllocateFundIdAsync(CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return null;
        var result = await _identities.AllocateFundIdAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Value is not { Kind: PortfolioBusinessIdentityKind.Fund, Value: > 0 } value)
        {
            MapError(result.ErrorCode, result.ErrorMessage);
            return null;
        }
        return value.Value;
    }

    public async Task<bool> CreatePortfolioAsync(PortfolioReadModel portfolio, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return false;
        var errors = portfolio.Validate(requireActivePolicy: false);
        if (errors.Count > 0) { Validation(errors); return false; }
        return await ExecuteAsync(_commands.CreatePortfolioAsync(portfolio, Guid.NewGuid(), cancellationToken), $"Portfolio {portfolio.PortfolioId} committed; waiting for projection.").ConfigureAwait(false);
    }

    public async Task<bool> AddPortfolioVersionAsync(PortfolioReadModel portfolio, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return false;
        var errors = portfolio.Validate(requireActivePolicy: portfolio.OperatingState == PortfolioOperatingState.Active);
        if (errors.Count > 0) { Validation(errors); return false; }
        return await ExecutePortfolioAsync(_commands.AddPortfolioVersionAsync(portfolio, RequirePortfolioRevision(), cancellationToken), $"Portfolio {portfolio.PortfolioId} version committed.").ConfigureAwait(false);
    }

    public async Task<bool> ChangePortfolioStateAsync(PortfolioOperatingState state, string reason, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation() || SelectedPortfolio is null) return false;
        return await ExecutePortfolioAsync(_commands.ChangePortfolioStateAsync(new PortfolioId(SelectedPortfolio.PortfolioId), RequirePortfolioRevision(), state, reason, cancellationToken), $"Portfolio {SelectedPortfolio.PortfolioId} state change committed.").ConfigureAwait(false);
    }

    public async Task<bool> DeleteDraftPortfolioAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation() || SelectedPortfolio is null) return false;
        if (SelectedPortfolio.OperatingState != PortfolioOperatingState.Draft)
        {
            Validation(["Only a Draft Portfolio can be deleted."]);
            return false;
        }
        var id = SelectedPortfolio.PortfolioId;
        var result = await _commands.DeleteDraftPortfolioAsync(new PortfolioId(id), RequirePortfolioRevision(), reason, cancellationToken).ConfigureAwait(false);
        if (!Finish(result, $"Draft Portfolio {id} deleted; its integer ID remains consumed.")) return false;
        SelectedPortfolio = null; SelectedFund = null; Funds = []; PortfolioRevision = 0; FundRevision = 0; ClearConfiguration();
        return true;
    }

    public async Task<bool> CreateFundAsync(FundMandateReadModel mandate, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation() || SelectedPortfolio is null) return false;
        var errors = mandate.Validate();
        if (errors.Count > 0) { Validation(errors); return false; }
        var create = await _fundCommands.CreateFundMandateAsync(mandate, Guid.NewGuid(), cancellationToken).ConfigureAwait(false);
        if (!create.Success) { MapError(create.ErrorCode, create.ErrorMessage); return false; }
        var attach = await _commands.AddFundAsync(new(mandate.PortfolioId, mandate.FundId), RequirePortfolioRevision(), cancellationToken).ConfigureAwait(false);
        return FinishPortfolio(attach, $"Fund {mandate.FundId} created and attached; waiting for projection.");
    }

    public async Task<bool> AddFundVersionAsync(FundMandateReadModel mandate, long expectedVersion, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return false;
        var errors = mandate.Validate();
        if (errors.Count > 0) { Validation(errors); return false; }
        return await ExecuteFundAsync(_fundCommands.AddFundMandateVersionAsync(mandate, RequireFundRevision(), cancellationToken), $"Fund {mandate.FundId} version committed.").ConfigureAwait(false);
    }

    public async Task<bool> ChangeFundStateAsync(FundOperatingState state, string reason, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation() || SelectedFund is null) return false;
        return await ExecuteFundAsync(_fundCommands.ChangeFundStateAsync(new(SelectedFund.PortfolioId, SelectedFund.FundId), RequireFundRevision(), state, reason, cancellationToken), $"Fund {SelectedFund.FundId} state change committed.").ConfigureAwait(false);
    }

    public async Task<bool> DelegateAllocationAsync(FundAllocationReadModel allocation, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return false;
        var errors = allocation.Validate(); if (errors.Count > 0) { Validation(errors); return false; }
        return await ExecutePortfolioAsync(_commands.DelegateAllocationAsync(allocation, RequirePortfolioRevision(), cancellationToken), $"Allocation {allocation.AllocationVersion} committed.").ConfigureAwait(false);
    }

    public async Task<bool> DelegateRiskEnvelopeAsync(FundRiskEnvelopeReadModel envelope, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return false;
        var errors = envelope.Validate(); if (errors.Count > 0) { Validation(errors); return false; }
        return await ExecutePortfolioAsync(_commands.DelegateRiskEnvelopeAsync(envelope, RequirePortfolioRevision(), cancellationToken), $"Risk envelope {envelope.EnvelopeVersion} committed.").ConfigureAwait(false);
    }

    public async Task<bool> AssignTradeTemplateAsync(FundTradeTemplateAssignmentReadModel assignment, CancellationToken cancellationToken = default)
    {
        if (!EnsureMutation()) return false;
        var errors = assignment.Validate(); if (errors.Count > 0) { Validation(errors); return false; }
        return await ExecuteFundAsync(_fundCommands.AssignTradeTemplateAsync(assignment, RequireFundRevision(), cancellationToken), $"Trade template assignment {assignment.AssignmentVersion} committed.").ConfigureAwait(false);
    }

    async Task<bool> ExecuteAsync(Task<ServiceResult<Guid>> operation, string message) => Finish(await operation.ConfigureAwait(false), message);
    async Task<bool> ExecutePortfolioAsync(Task<ServiceResult<Guid>> operation, string message) => FinishPortfolio(await operation.ConfigureAwait(false), message);
    async Task<bool> ExecuteFundAsync(Task<ServiceResult<Guid>> operation, string message) => FinishFund(await operation.ConfigureAwait(false), message);
    bool FinishPortfolio(ServiceResult<Guid> result, string message) { var success = Finish(result, message); if (success) PortfolioRevision++; return success; }
    bool FinishFund(ServiceResult<Guid> result, string message) { var success = Finish(result, message); if (success) FundRevision++; return success; }

    bool Finish(ServiceResult<Guid> result, string message)
    {
        if (!result.Success) { MapError(result.ErrorCode, result.ErrorMessage); return false; }
        State = PortfolioUiState.PendingProjection; Message = message; return true;
    }

    bool EnsureMutation()
    {
        if (CanMutate) return true;
        State = PortfolioUiState.Unauthorized; Message = "Portfolio mutation permission is required."; return false;
    }

    long RequirePortfolioRevision() => PortfolioRevision > 0 ? PortfolioRevision : throw new InvalidOperationException("Select a current Portfolio projection before changing it.");
    long RequireFundRevision() => FundRevision > 0 ? FundRevision : throw new InvalidOperationException("Select a current Fund projection before changing it.");

    void ClearConfiguration() { Allocation = null; RiskEnvelope = null; Assignments = []; }
    void Validation(IReadOnlyList<string> errors) { State = PortfolioUiState.ValidationError; Message = string.Join("; ", errors); }

    void MapError(int code, string message)
    {
        State = code switch
        {
            34002 or 34012 or 34015 => PortfolioUiState.ValidationError,
            34003 or 34006 => PortfolioUiState.Conflict,
            34198 => PortfolioUiState.Timeout,
            34199 or 34014 => PortfolioUiState.Unavailable,
            34290 => PortfolioUiState.Unauthorized,
            _ => PortfolioUiState.Unavailable,
        };
        Message = string.IsNullOrWhiteSpace(message) ? "Portfolio operation failed." : message;
    }
}
