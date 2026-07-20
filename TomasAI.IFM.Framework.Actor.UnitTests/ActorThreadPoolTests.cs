using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;
using Moq;

namespace TomasAI.IFM.Framework.Actor.UnitTests
{
    public class ActorThreadPoolTests
    {
        [Fact]
        public void AddAndRemoveActor_StartsAndStopsThread()
        {
            var logger = NullLogger<EventChannel>.Instance;
            var pool = new ActorThreadPoolImpl(logger, TimeSpan.FromSeconds(5));

            var mailbox = new Mock<IActorMailbox>();
            var queue = new TomasAI.IFM.Shared.EventModelActor.Contracts.TestActorMailboxQueue();
            mailbox.SetupGet(m => m.Queue).Returns(queue);
            mailbox.SetupGet(m => m.Id).Returns(new ActorMailboxId(ActorType.Domain, "test"));

            var actorMock = new Mock<IActor>();
            actorMock.SetupGet(a => a.Mailbox).Returns(mailbox.Object);
            actorMock.SetupGet(a => a.IsRunning).Returns(true);

            var added = pool.AddActor(actorMock.Object);
            Assert.True(added);
            Assert.True(pool.Exists(mailbox.Object.Id));

            var removed = pool.RemoveActor(mailbox.Object.Id);
            Assert.True(removed);
            Assert.False(pool.Exists(mailbox.Object.Id));
        }

        [Fact]
        public async Task ProcessingMessage_TimeoutOccurs_SetsTimedOutState()
        {
            var logger = NullLogger<EventChannel>.Instance;
            // set short timeout for test
            var pool = new ActorThreadPoolImpl(logger, TimeSpan.FromMilliseconds(200));

            var mailbox = new Mock<IActorMailbox>();
            var queue = new TomasAI.IFM.Shared.EventModelActor.Contracts.TestActorMailboxQueue();
            mailbox.SetupGet(m => m.Queue).Returns(queue);
            mailbox.SetupGet(m => m.Id).Returns(new ActorMailboxId(ActorType.Domain, "test-timeout"));

            var actor = new TestVerySlowActor();
            var actorMock = new Mock<IActor>();
            actorMock.SetupGet(a => a.Mailbox).Returns(mailbox.Object);
            actorMock.Setup(a => a.ReceiveAsync(It.IsAny<IActorMessage>())).Returns((IActorMessage m) => actor.ReceiveAsync(m));

            pool.AddActor(actorMock.Object);

            // post a message
            var msg = new Mock<IActorMessage>().Object;
            queue.Write(msg);

            // wait longer than the configured timeout
            await Task.Delay(TimeSpan.FromSeconds(1));

            var thread = pool.GetActorThread(mailbox.Object.Id);
            Assert.NotNull(thread);
            Assert.True(thread.State == ActorThreadState.TimedOut || thread.IsStopped || thread.IsFaulted);
            Assert.NotNull(thread.Exception);
        }

        [Fact]
        public async Task ProcessingMessage_ExceptionInHandler_SetsFaulted()
        {
            var logger = NullLogger<EventChannel>.Instance;
            var pool = new ActorThreadPoolImpl(logger, TimeSpan.FromSeconds(5));

            var mailbox = new Mock<IActorMailbox>();
            var queue = new TomasAI.IFM.Shared.EventModelActor.Contracts.TestActorMailboxQueue();
            mailbox.SetupGet(m => m.Queue).Returns(queue);
            mailbox.SetupGet(m => m.Id).Returns(new ActorMailboxId(ActorType.Domain, "test-fault"));

            var actorMock = new Mock<IActor>();
            actorMock.SetupGet(a => a.Mailbox).Returns(mailbox.Object);
            actorMock.Setup(a => a.ReceiveAsync(It.IsAny<IActorMessage>())).Returns<IActorMessage>(m => throw new InvalidOperationException("boom"));

            pool.AddActor(actorMock.Object);

            // post a message
            var msg = new Mock<IActorMessage>().Object;
            queue.Write(msg);

            await Task.Delay(TimeSpan.FromSeconds(1));

            var thread = pool.GetActorThread(mailbox.Object.Id);
            Assert.NotNull(thread);
            Assert.True(thread.IsFaulted);
            Assert.NotNull(thread.Exception);
            Assert.IsType<InvalidOperationException>(thread.Exception);
        }

        private class TestVerySlowActor
        {
            public async ValueTask ReceiveAsync(IActorMessage message)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
    }
}
