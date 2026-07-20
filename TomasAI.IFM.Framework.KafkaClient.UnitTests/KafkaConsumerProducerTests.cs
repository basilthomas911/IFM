using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Confluent.Kafka;
using Newtonsoft.Json;
using NSubstitute;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.KafkaClient;
using TomasAI.IFM.Shared.MarketDataFeed.Events;

namespace TomasAI.IFM.Framework.KafkaClient.UnitTests
{
    public class KafkaConsumerProducerTests
    {
        [Fact]
        public async Task ConsumerProducerOk()
        {
            // create consumer options...
            var consumerOptions = new KafkaTestEventConsumerOptions(groupId: null, bootstrapServers: "localhost:9092", enableAutoCommit: true);

            // create consumer...
            var eventIn = new MarketDataFeedStartedEvent { StartedBy = "basilt", StartedOn = new DateTime(2018, 10, 12) };
            var eventOut = default(MarketDataFeedStartedEvent);
            var consumer = new KafkaTestConsumer(consumerOptions, null);
            consumer.SetConsoleWrtiter(e => eventOut = e);
            await consumer.StartAsync();
            await Task.Delay(TimeSpan.FromSeconds(2));

            // create producer options...
            var producerOptions = new KafkaTestEventProducerOptions(bootstrapServers: "localhost:9092");

            // create producer...
            var logger = Substitute.For<ILogger>();
            logger.When(e => e.LogError(Arg.Any<Exception>(), Arg.Any<string>()))
                .Do(e => { });
            var producer = new KafkaTestProducer(producerOptions, logger);
            await producer.ProduceAsync("1234", new MarketDataFeedStartedEvent { StartedBy = "basilt", StartedOn = DateTime.Now });
            await Task.Delay(TimeSpan.FromSeconds(2));

            eventOut.Should().NotBeNull();
            eventOut.StartedBy.Should().Be(eventIn.StartedBy);
            await consumer.StopAsync();
        }
    }
}
