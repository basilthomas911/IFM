using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.Reference;

/// <summary>Maps typed Reference APIs and lookup events into UI-owned operations and models.</summary>
public sealed class ReferenceDataService(
    IReferenceCommandApi commandApi,
    IReferenceQueryApi queryApi,
    ILookupTypeUIEventConsumer eventConsumer) : IReferenceDataService
{
    readonly IReferenceCommandApi _commandApi =
        commandApi ?? throw new ArgumentNullException(nameof(commandApi));
    readonly IReferenceQueryApi _queryApi =
        queryApi ?? throw new ArgumentNullException(nameof(queryApi));
    readonly ILookupTypeUIEventConsumer _eventConsumer =
        eventConsumer ?? throw new ArgumentNullException(nameof(eventConsumer));

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<Guid>> AddLookupTypeAsync(
        LookupTypeUiModel lookupType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _commandApi.AddLookupTypeAsync(ToBackend(lookupType)).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<Guid>> ChangeLookupTypeAsync(
        string lookupTypeName,
        int orderId,
        LookupTypeUiModel lookupType,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _commandApi.ChangeLookupTypeAsync(
            new LookupTypeId(lookupTypeName, orderId),
            ToBackend(lookupType),
            overwrite).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<Guid>> RemoveLookupTypeAsync(
        string lookupTypeName,
        int orderId,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _commandApi.RemoveLookupTypeAsync(
            new LookupTypeId(lookupTypeName, orderId),
            overwrite).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>> GetLookupTypesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _queryApi.GetLookupTypesAsync().ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>> GetLookupTypesAsync(
        string lookupTypeName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _queryApi.GetLookupTypesAsync(lookupTypeName).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<string>>> GetLookupTypeNamesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await _queryApi.GetLookupTypeNamesAsync().ConfigureAwait(false))
            .ToUiResult(values => (IReadOnlyList<string>)values);
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<LookupTypeShortCodeUiModel>>>
        GetLookupTypeShortCodesAsync(
            string lookupTypeName,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await _queryApi.GetLookupTypeShortCodesAsync(lookupTypeName).ConfigureAwait(false))
            .ToUiResult(values => (IReadOnlyList<LookupTypeShortCodeUiModel>)values
                .Select(value => new LookupTypeShortCodeUiModel(value.ShortCode, value.OrderId))
                .ToArray());
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>>
        GetMarketDataDefinitionTypesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _queryApi.GetMarketDataDefinitionTypesAsync().ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>>
        GetReferenceDataDefinitionTypesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _queryApi.GetReferenceDataDefinitionTypesAsync().ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>>
        GetSystemAdminFunctionTypesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Map(await _queryApi.GetSystemAdminFunctionTypesAsync().ConfigureAwait(false));
    }

    /// <inheritdoc />
    public ValueTask<UiOperationResult<int>> GetNextFundIdAsync(CancellationToken cancellationToken = default)
        => GetNextIdAsync("FundId", cancellationToken);

    /// <inheritdoc />
    public ValueTask<UiOperationResult<int>> GetNextOrderIdAsync(CancellationToken cancellationToken = default)
        => GetNextIdAsync("OrderId", cancellationToken);

    /// <inheritdoc />
    public ValueTask<UiOperationResult<int>> GetNextTradeIdAsync(CancellationToken cancellationToken = default)
        => GetNextIdAsync("TradeId", cancellationToken);

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DefaultFuturesContractDefinitionsUiModel>>
        GetDefaultFuturesContractDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await _queryApi.GetDefaultFuturesContractDefinitionsAsync().ConfigureAwait(false))
            .ToUiResult(value => new DefaultFuturesContractDefinitionsUiModel(
                value.Currency,
                value.Exchange,
                value.Multiplier,
                value.SecurityType,
                value.OptionSecurityType,
                value.Symbol));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<FuturesOptionStrikePriceUiModel>>
        GetFuturesOptionStrikePriceDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await _queryApi.GetFuturesOptionStrikePriceDefinitionsAsync().ConfigureAwait(false))
            .ToUiResult(value => new FuturesOptionStrikePriceUiModel(
                value.Minimum,
                value.Maximum,
                value.Increment));
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<IReadOnlyList<MdiForwardLossRatioUiModel>>>
        GetMdiForwardLossRatiosAsync(
            IntrinsicTimeTrendType trendDirection,
            TradeType tradeType,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await _queryApi.GetMDIForwardLossRatiosAsync(trendDirection, tradeType).ConfigureAwait(false))
            .ToUiResult(values => (IReadOnlyList<MdiForwardLossRatioUiModel>)values
                .Select(value => new MdiForwardLossRatioUiModel(
                    value.MDI,
                    value.TrendDirection,
                    value.TradeType,
                    value.ForwardLossRatio,
                    value.CreatedBy,
                    value.CreatedOn,
                    value.UpdatedBy,
                    value.UpdatedOn))
                .ToArray());
    }

    /// <inheritdoc />
    public IUiEventSubscription CreateLookupSubscription(
        Func<TerminalNotificationUiModel, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new OwnedUiEventSubscription(
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _eventConsumer.StartAsync(value => handler(ToTerminal(value)));
            },
            _eventConsumer.StopAsync);
    }

    async ValueTask<UiOperationResult<int>> GetNextIdAsync(
        string seedType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (await _queryApi.GetNextSeedIdAsync(seedType).ConfigureAwait(false))
            .ToUiResult(value => value.Value);
    }

    static UiOperationResult<IReadOnlyList<LookupTypeUiModel>> Map(
        ServiceResult<LookupTypeCollection> result)
        => result.ToUiResult(values => (IReadOnlyList<LookupTypeUiModel>)values
            .Select(ToUi)
            .ToArray());

    static UiOperationResult<Guid> Map(ServiceResult<Guid> result)
        => result.ToUiResult(value => value);

    static LookupTypeUiModel ToUi(LookupTypeReadModel value)
        => new(
            value.LookupTypeName,
            value.ShortCode,
            value.OrderId,
            value.Description,
            value.CreatedOn,
            value.CreatedBy);

    static LookupTypeReadModel ToBackend(LookupTypeUiModel value)
        => new(
            value.LookupTypeName,
            value.ShortCode,
            value.OrderId,
            value.Description,
            value.CreatedOn,
            value.CreatedBy);

    static TerminalNotificationUiModel ToTerminal(IEvent value)
        => value is IErrorEvent error
            ? new TerminalNotificationUiModel(
                value.CommandId,
                error.ErrorCode,
                error.ErrorMessage,
                GetKind(value))
            : new TerminalNotificationUiModel(value.CommandId, Kind: GetKind(value));

    static TerminalNotificationKind GetKind(IEvent value)
        => value switch
        {
            LookupTypeAddedCompleteEvent or LookupTypeAddedFailEvent
                => TerminalNotificationKind.Added,
            LookupTypeChangedCompleteEvent or LookupTypeChangedFailEvent
                => TerminalNotificationKind.Changed,
            LookupTypeRemovedCompleteEvent or LookupTypeRemovedFailEvent
                => TerminalNotificationKind.Removed,
            _ => TerminalNotificationKind.Unknown
        };
}
