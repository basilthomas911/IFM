using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.EventSourcing;

internal static class EventLogBinaryReader
{
    internal static IEvent Read(string typeName, long version, byte[] payload, IEventTypeResolver? resolver = null)
    {
        resolver ??= EventTypeRegistry.Default;
        var legacyPortfolioEvent = typeName.StartsWith(
            "TomasAI.IFM.Domain.Portfolio.Command.Model.", StringComparison.Ordinal);
        // Explicit old-name registrations take priority over the existing Portfolio compatibility route.
        var type = resolver.Resolve(typeName);
        if (type is null && legacyPortfolioEvent)
            type = resolver.Resolve(ResolveLegacyPortfolioEventType(typeName));
        // An unavailable event type remains observable. Corrupt known events must fail replay.
        if (type is null)
            return new UnknownEvent(subject: default, id: Guid.Empty, entityId: default,
                eventId: version, commandId: Guid.Empty, aggregateId: string.Empty,
                eventSource: string.Empty, receivedOn: DateTime.MinValue, eventSourceId: 0,
                eventSourceVersion: 0, eventTypeName: typeName,
                eventData: Convert.ToBase64String(payload), eventDate: DateTime.MinValue);
        return EventLogMessagePackCodec.Shared.DeserializeResolved(type, version, payload, legacyPortfolioEvent);
    }

    static string ResolveLegacyPortfolioEventType(string typeName)
    {
        const string prefix = "TomasAI.IFM.Domain.Portfolio.Command.Model.";
        if (!typeName.StartsWith(prefix, StringComparison.Ordinal)) return typeName;

        var separator = typeName.IndexOf(',', StringComparison.Ordinal);
        var legacyName = typeName[prefix.Length..(separator < 0 ? typeName.Length : separator)];
        var targetNamespace = legacyName switch
        {
            "FundMandateCreated" or "FundMandateVersionAdded" or "FundOperatingStateChanged"
                or "FundTradeTemplateAssigned" or "FundCompositionReserved"
                or "FundCompositionStateChanged" or "FundManualOrderChanged" or "FundManualOrderDeleted"
                => "TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events",
            "PortfolioFinancialPolicyCreated" or "PortfolioFinancialPolicyActivated"
                or "PortfolioFinancialPolicyVersionAdded" or "PortfolioFinancialPolicyRetired"
                or "DraftPortfolioFinancialPolicyDeleted"
                => "TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events",
            _ => "TomasAI.IFM.Domain.Portfolio.Shared.Events",
        };
        return $"{targetNamespace}.{legacyName}Event, TomasAI.IFM.Domain.Portfolio.Shared";
    }
}
