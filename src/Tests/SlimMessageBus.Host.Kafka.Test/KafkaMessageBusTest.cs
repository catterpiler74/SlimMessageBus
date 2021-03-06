﻿using Confluent.Kafka;
using FluentAssertions;
using Moq;
using SlimMessageBus.Host.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaMessageBusTest : IDisposable
    {
        MessageBusSettings MbSettings { get; }
        KafkaMessageBusSettings KafkaMbSettings { get; }
        Lazy<WrappedKafkaMessageBus> KafkaMb { get; }

        public KafkaMessageBusTest()
        {
            MbSettings = new MessageBusSettings
            {
                Serializer = new Mock<IMessageSerializer>().Object,
                DependencyResolver = new Mock<IDependencyResolver>().Object
            };
            KafkaMbSettings = new KafkaMessageBusSettings("host")
            {
                ProducerFactory = (producerSettings) => new Mock<Producer>(Enumerable.Empty<KeyValuePair<string, object>>()).Object
            };
            KafkaMb = new Lazy<WrappedKafkaMessageBus>(() => new WrappedKafkaMessageBus(MbSettings, KafkaMbSettings));
        }

        public void Dispose()
        {
            KafkaMb.Value.Dispose();
        }

        [Fact]
        public void GetMessageKey()
        {
            // arrange
            var publisherA = new PublisherSettings();
            new PublisherBuilder<MessageA>(publisherA)
                .KeyProvider((m, t) => m.Key);

            var publisherB = new PublisherSettings();
            new PublisherBuilder<MessageB>(publisherB);

            MbSettings.Publishers.Add(publisherA);
            MbSettings.Publishers.Add(publisherB);        

            var msgA = new MessageA();
            var msgB = new MessageB();

            // act
            var msgAKey = KafkaMb.Value.Public_GetMessageKey(msgA.GetType(), msgA, "topic1");
            var msgBKey = KafkaMb.Value.Public_GetMessageKey(msgB.GetType(), msgB, "topic1");

            // assert
            msgAKey.Should().BeSameAs(msgA.Key);
            msgBKey.Should().BeNull();
        }

        [Fact]
        public void GetMessagePartition()
        {
            // arrange
            var publisherA = new PublisherSettings();
            new PublisherBuilder<MessageA>(publisherA)
                .PartitionProvider((m, t) => 10);

            var publisherB = new PublisherSettings();
            new PublisherBuilder<MessageB>(publisherB);

            MbSettings.Publishers.Add(publisherA);
            MbSettings.Publishers.Add(publisherB);

            var msgA = new MessageA();
            var msgB = new MessageB();

            // act
            var msgAPartition = KafkaMb.Value.Public_GetMessagePartition(msgA.GetType(), msgA, "topic1");
            var msgBPartition = KafkaMb.Value.Public_GetMessagePartition(msgB.GetType(), msgB, "topic1");

            // assert
            msgAPartition.Should().Be(10);
            msgBPartition.Should().Be(-1);
        }

        class MessageA
        {
            public byte[] Key { get; } = Guid.NewGuid().ToByteArray();
        }

        class MessageB
        {
        }

        class WrappedKafkaMessageBus : KafkaMessageBus
        {
            public WrappedKafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings kafkaSettings)
                : base(settings, kafkaSettings)
            {
            }

            public byte[] Public_GetMessageKey(Type messageType, object message, string topic)
            {
                return GetMessageKey(messageType, message, topic);
            }

            public int Public_GetMessagePartition(Type messageType, object message, string topic)
            {
                return GetMessagePartition(messageType, message, topic);
            }
        }
    }
}
