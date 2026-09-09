using System.Globalization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Ordered List validation extensions shared by financial actor maps.</summary>
public static class FinancialRequestValidation
{
    public static List<ValidationError> ValidateFinancialRequest<TRequest,TBody>(this List<ValidationError> errors,
        TRequest request, ActorType actorType, string actorName, string verb)
        where TRequest : class, IFinancialRequest<TBody>
    {
        Add(request.SchemaVersion == 1, "SchemaVersion must be 1.");
        Add(request.CommandId != Guid.Empty && request.OperationId != Guid.Empty && request.PortfolioId > 0, "Financial identities are required.");
        Add(request.CorrelationId != Guid.Empty && request.CausationId != Guid.Empty, "Correlation and causation are required.");
        Add(request.Subject.ActorType == actorType && request.Subject.Name == actorName && request.Subject.Verb == verb,
            "Financial request actor/verb is incorrect.");
        Add(request.ExpiresAtUtc.Kind == DateTimeKind.Utc && request.RequestedAtUtc.Kind == DateTimeKind.Utc &&
            request.ExpiresAtUtc > request.RequestedAtUtc, "Fixed UTC request/expiry times are required.");
        Add(request.ExpectedFinancialRevision >= 0, "Financial revision must be nonnegative.");
        Add(request.Access is not null && !string.IsNullOrWhiteSpace(request.Access.Principal) && request.Access.Roles is not null,
            "Authenticated caller metadata is required.");
        Add(request.Body is not null, "A typed financial payload is required.");
        var identity = request switch
        {
            SubmitEmulatorOrderCommand x when x.EntityId.PortfolioId == x.PortfolioId => x.EntityId.Format(),
            PostFundTransactionCommand x when x.EntityId.PortfolioId == x.PortfolioId => x.EntityId.Format(),
            PostFundTransactionsCommand x when x.EntityId.PortfolioId == x.PortfolioId => x.EntityId.Format(),
            ConfigureLedgerCommand x when x.EntityId.PortfolioId == x.PortfolioId => x.EntityId.Format(),
            ReservePortfolioTradeRiskCommand x when x.EntityId.PortfolioId == x.PortfolioId && x.EntityId.OperationId == x.OperationId => x.EntityId.Format(),
            ConsumeCapacityReservationCommand x when x.EntityId.PortfolioId == x.PortfolioId && x.EntityId.OperationId == x.OperationId => x.EntityId.Format(),
            ChangeCapacityReservationCommand x when x.Body is not null && x.EntityId.PortfolioId == x.PortfolioId && x.EntityId.ReservationId == x.Body.ReservationId => x.EntityId.Format(),
            _ => null
        };
        Add(identity is not null && request.Subject.EntityId == identity, "Subject, aggregate and business identities must agree.");
        Add(request.PostEvents == (actorType == ActorType.Command), "Only financial Commands publish durable notifications.");
        var permission = request switch
        {
            SubmitEmulatorOrderCommand => "EmulatorSubmit",
            ReservePortfolioTradeRiskCommand => "CapacityReserve", ConsumeCapacityReservationCommand => "CapacityConsume",
            ChangeCapacityReservationCommand => "CapacityLifecycle", ConfigureLedgerCommand => "LedgerConfigure", _ => "LedgerPost"
        };
        Add(request.Access?.Roles is { } roles && (roles.Contains("PortfolioAdministrator", StringComparer.Ordinal) || roles.Contains(permission, StringComparer.Ordinal)),
            "Caller lacks financial operation permission.");
        Add(request.Access?.Roles?.Contains("PortfolioAdministrator", StringComparer.Ordinal)==true ||
            request.Access?.PortfolioIds?.Contains(request.PortfolioId)==true,"Caller is not authorized for this Portfolio.");
        if (errors.Count == 0)
        {
            Add(request.InputSha256 == FinancialCanonicalHash.Request(request), "Input fingerprint does not match the canonical request.");
            Add(MessagePackBinarySerializer.MeasureContent(request) <= 1048576 && MessagePackBinarySerializer.MeasureEncoded(request) <= 1048576,
                "Financial request exceeds 1 MiB.");
        }
        return errors;
        void Add(bool valid, string message)
        { if (!valid) errors.Add(new(FinancialReasons.InvalidContract.ToString(CultureInfo.InvariantCulture), message)); }
    }

    public static void Demand(IFinancialRequest request, string permission, DateTime nowUtc)
    {
        if (request.Access is null || string.IsNullOrWhiteSpace(request.Access.Principal) ||
            !(request.Access.Roles.Contains("PortfolioAdministrator", StringComparer.Ordinal) || request.Access.Roles.Contains(permission, StringComparer.Ordinal)))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied, "Caller lacks financial operation permission.");
        if (nowUtc >= request.ExpiresAtUtc)
            throw new FinancialOperationException(FinancialReasons.TimeExpired, "Financial request deadline expired.");
    }

    public static bool DemandPosting(IFinancialRequest request,LedgerPostingRequest body,DateTime nowUtc)
    {
        Demand(request,"LedgerPost",nowUtc);
        var permission=body.TransactionKind switch
        {
            LedgerTransactionKind.OpeningBalance=>"LedgerImport",
            LedgerTransactionKind.Adjustment=>"LedgerAdjust",
            LedgerTransactionKind.Reversal=>"LedgerReverse",
            _=>null
        };
        if(permission is not null) Demand(request,permission,nowUtc);
        return body.TransactionKind is LedgerTransactionKind.OpeningBalance or LedgerTransactionKind.Adjustment;
    }
}

