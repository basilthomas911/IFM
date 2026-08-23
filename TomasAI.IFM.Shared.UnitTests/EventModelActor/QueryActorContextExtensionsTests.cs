using FluentAssertions;
using Moq;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

/// <summary>
/// Verifies the transitional reply-metadata bridge used by typed query actor contexts.
/// </summary>
public sealed class QueryActorContextExtensionsTests
{
    [Fact]
    public void MirrorMessageInfoTo_copies_and_cleans_target_without_removing_source()
    {
        var source = new Mock<IQueryActorContext>(MockBehavior.Strict);
        var target = new Mock<IQueryActorContext>(MockBehavior.Strict);
        var threadId = new ActorThreadId(ActorType.Query, "TestQuery", "1");
        const string verb = "GetTest";
        var messageInfo = new ActorMessageInfo(
            Mock.Of<IActorMessage>(),
            Mock.Of<IQuery>());
        source.Setup(context => context.GetMessageInfo(threadId, verb))
            .Returns(messageInfo);
        target.Setup(context => context.SetMessageInfo(threadId, verb, messageInfo))
            .Returns(true);
        target.Setup(context => context.RemoveMessageInfo(threadId, verb))
            .Returns(true);

        using (source.Object.MirrorMessageInfoTo(target.Object, threadId, verb))
        {
            target.Verify(
                context => context.SetMessageInfo(threadId, verb, messageInfo),
                Times.Once);
            target.Verify(
                context => context.RemoveMessageInfo(threadId, verb),
                Times.Never);
        }

        target.Verify(
            context => context.RemoveMessageInfo(threadId, verb),
            Times.Once);
        source.Verify(
            context => context.RemoveMessageInfo(It.IsAny<ActorThreadId>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void MirrorMessageInfoTo_returns_empty_scope_when_source_and_target_are_identical()
    {
        var context = new Mock<IQueryActorContext>(MockBehavior.Strict);
        var threadId = new ActorThreadId(ActorType.Query, "TestQuery", "1");

        var action = () =>
        {
            using var scope = context.Object.MirrorMessageInfoTo(
                context.Object,
                threadId,
                "GetTest");
        };

        action.Should().NotThrow();
        context.VerifyNoOtherCalls();
    }
}
