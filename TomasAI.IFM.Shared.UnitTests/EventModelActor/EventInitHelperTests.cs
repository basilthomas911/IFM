using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class EventInitHelperTests
{
    [Fact]
    public void TypedSetters_AssignActualEventMetadata()
    {
        var value = new LookupTypeAddedEvent();
        var commandId = Guid.NewGuid();
        EventInitHelper.SetProperty(value, nameof(value.CommandId), commandId);
        EventInitHelper.SetProperty(value, nameof(value.AggregateId), "LookupType.Exchange");
        EventInitHelper.SetProperty(value, nameof(value.EventSource), "ReferenceEvents");

        value.CommandId.Should().Be(commandId);
        value.AggregateId.Should().Be("LookupType.Exchange");
        value.EventSource.Should().Be("ReferenceEvents");
    }

    [Fact]
    public void ConcurrentSetters_DoNotCaptureOtherTargetsOrValues()
    {
        Parallel.For(0, 512, i =>
        {
            var target = new Properties();
            EventInitHelper.SetProperty(target, nameof(target.Number), i);
            EventInitHelper.SetProperty(target, nameof(target.Text), i.ToString());
            target.Number.Should().Be(i);
            target.Text.Should().Be(i.ToString());
        });
    }

    [Fact]
    public void ReflectionFallback_PreservesConversionsMissingAndPrivateSetters()
    {
        var target = new Properties();
        EventInitHelper.SetProperty(target, "Missing", 1);
        EventInitHelper.SetProperty(target, nameof(target.Number), (short)7);
        EventInitHelper.SetProperty(target, nameof(target.PrivateNumber), 8);
        EventInitHelper.SetProperty<object>(target, nameof(target.Text), "object string");
        EventInitHelper.SetProperty<string>(target, nameof(target.Text), null);

        target.Number.Should().Be(7);
        target.PrivateNumber.Should().Be(8);
        target.Text.Should().BeNull();
    }

    [Fact]
    public void ReflectionFallback_UpdatesOriginalBoxedStruct()
    {
        object target = new ValueProperties();
        EventInitHelper.SetProperty(target, nameof(ValueProperties.Number), 42);
        ((ValueProperties)target).Number.Should().Be(42);
    }

    [Fact]
    public void Errors_PreserveReflectionExceptionContract()
    {
        var target = new Properties();
        Action throwing = () => EventInitHelper.SetProperty(target, nameof(target.Throwing), 1);
        throwing.Should().Throw<TargetInvocationException>().WithInnerException<InvalidOperationException>();
        Action incompatible = () => EventInitHelper.SetProperty(target, nameof(target.Number), "invalid");
        incompatible.Should().Throw<ArgumentException>();
        Action readOnly = () => EventInitHelper.SetProperty(target, nameof(target.ReadOnly), 1);
        readOnly.Should().Throw<ArgumentException>();
    }

    public sealed class Properties
    {
        public int Number { get; init; }
        public string Text { get; set; }
        public int PrivateNumber { get; private set; }
        public int ReadOnly => 0;
        public int Throwing { get => 0; set => throw new InvalidOperationException("setter failed"); }
    }

    public struct ValueProperties
    {
        public int Number { get; set; }
    }
}
