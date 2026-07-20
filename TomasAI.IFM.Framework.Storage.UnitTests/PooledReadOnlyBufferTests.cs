using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class PooledReadOnlyBufferTests
{
    static PooledReadOnlyBuffer<int> CreateBuffer(params int[] values)
    {
        var owner = MemoryPool<int>.Shared.Rent(values.Length);
        values.AsSpan().CopyTo(owner.Memory.Span);
        return new PooledReadOnlyBuffer<int>(owner, values.Length);
    }

    [Fact]
    public void CountReturnsCorrectValue()
    {
        using var buffer = CreateBuffer(1, 2, 3);
        buffer.Count.Should().Be(3);
    }

    [Fact]
    public void IndexerReturnsCorrectValue()
    {
        using var buffer = CreateBuffer(10, 20, 30);
        buffer[0].Should().Be(10);
        buffer[1].Should().Be(20);
        buffer[2].Should().Be(30);
    }

    [Fact]
    public void IndexerThrowsForNegativeIndex()
    {
        using var buffer = CreateBuffer(1, 2, 3);
        var act = () => { _ = buffer[-1]; };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IndexerThrowsForIndexOutOfRange()
    {
        using var buffer = CreateBuffer(1, 2, 3);
        var act = () => { _ = buffer[3]; };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MemoryReturnsCorrectSlice()
    {
        using var buffer = CreateBuffer(5, 10, 15);
        var memory = buffer.Memory;
        memory.Length.Should().Be(3);
        memory.Span[0].Should().Be(5);
        memory.Span[1].Should().Be(10);
        memory.Span[2].Should().Be(15);
    }

    [Fact]
    public void SpanReturnsCorrectSlice()
    {
        using var buffer = CreateBuffer(5, 10, 15);
        var span = buffer.Span;
        span.Length.Should().Be(3);
        span[0].Should().Be(5);
        span[2].Should().Be(15);
    }

    [Fact]
    public void EnumeratorIteratesAllElements()
    {
        using var buffer = CreateBuffer(1, 2, 3, 4);
        var result = new List<int>();
        foreach (var item in buffer)
            result.Add(item);
        result.Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void IEnumerableIteratesAllElements()
    {
        using var buffer = CreateBuffer(10, 20);
        var result = ((IEnumerable<int>)buffer).ToList();
        result.Should().BeEquivalentTo(new[] { 10, 20 });
    }

    [Fact]
    public void DisposeReleasesResources()
    {
        var buffer = CreateBuffer(1, 2, 3);
        buffer.Dispose();
        var act = () => { _ = buffer.Count; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DoubleDisposeDoesNotThrow()
    {
        var buffer = CreateBuffer(1, 2);
        buffer.Dispose();
        buffer.Dispose();
    }

    [Fact]
    public void IndexerThrowsAfterDispose()
    {
        var buffer = CreateBuffer(1, 2, 3);
        buffer.Dispose();
        var act = () => { _ = buffer[0]; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MemoryThrowsAfterDispose()
    {
        var buffer = CreateBuffer(1, 2, 3);
        buffer.Dispose();
        var act = () => { _ = buffer.Memory; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void SpanThrowsAfterDispose()
    {
        var buffer = CreateBuffer(1, 2, 3);
        buffer.Dispose();
        var act = () => { _ = buffer.Span; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EmptyBufferCountIsZero()
    {
        using var buffer = CreateBuffer();
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void EmptyBufferEnumeratorYieldsNothing()
    {
        using var buffer = CreateBuffer();
        var result = new List<int>();
        foreach (var item in buffer)
            result.Add(item);
        result.Count.Should().Be(0);
    }

    [Fact]
    public void SingleElementBuffer()
    {
        using var buffer = CreateBuffer(42);
        buffer.Count.Should().Be(1);
        buffer[0].Should().Be(42);
    }

    [Fact]
    public void ConstructorThrowsWithNullOwner()
    {
        var act = () => { _ = new PooledReadOnlyBuffer<int>(null, 0); };
        act.Should().Throw<ArgumentNullException>();
    }
}
