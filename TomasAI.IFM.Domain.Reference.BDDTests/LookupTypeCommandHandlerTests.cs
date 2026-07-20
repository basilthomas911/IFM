using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Domain.Reference.LookupType;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;


namespace TomasAI.IFM.Domain.Reference.BDDTests;

public class LookupTypeCommandHandlerTests
{
}
