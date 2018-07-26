using System.Collections.Generic;
using System.IO;
using Autofac;
using Microsoft.Extensions.Configuration;
using Sample.Images.FileStore;
using Sample.Images.FileStore.Disk;
using Sample.Images.Messages;
using Sample.Images.Worker.Handlers;
using SlimMessageBus;
using SlimMessageBus.Host.Autofac;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.ServiceLocator;
using SlimMessageBus.Host.Kafka;
using Microsoft.Extensions.Configuration;

namespace Sample.Images.Worker
{
    public static class ContainerSetup
    {
        public static IContainer Create(IConfigurationRoot configuration)
        {
            var builder = new ContainerBuilder();

            Configure(builder, configuration);

            var container = builder.Build();

            AutofacMessageBusDependencyResolver.Container = container;

            // Set the service locator to an AutofacServiceLocator.
            /*
            var csl = new AutofacServiceLocator(container);
            ServiceLocator.SetLocatorProvider(() => csl);
            */

            return container;
        }

        private static void Configure(ContainerBuilder builder, IConfigurationRoot configuration)
        {
            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\Content");
            builder.Register(x => new DiskFileStore(imagesPath)).As<IFileStore>().SingleInstance();
            builder.RegisterType<SimpleThumbnailFileIdStrategy>().As<IThumbnailFileIdStrategy>().SingleInstance();

            // SlimMessageBus
            builder.Register(x => BuildMessageBus(configuration))
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.RegisterType<GenerateThumbnailRequestHandler>().AsSelf();
            //builder.RegisterType<GenerateThumbnailRequestSubscriber>().AsSelf();
        }

        private static IMessageBus BuildMessageBus(IConfigurationRoot configuration)
        {
            // unique id across instances of this application (e.g. 1, 2, 3)
            var instanceId = configuration["InstanceId"];
            var kafkaBrokers = configuration["Kafka:Brokers"];

            // configuration settings for Redis
            var redisServer = configuration["Redis:Server"];
            var redisSyncTimeout = 5000;
            int.TryParse(configuration["Redis:SyncTimeout"], out redisSyncTimeout);

            var instanceGroup = $"worker-{instanceId}";
            var sharedGroup = "workers";

            var messageBusBuilder = new MessageBusBuilder()
                .Handle<GenerateThumbnailRequest, GenerateThumbnailResponse>(s =>
                {
                    s.Topic("thumbnail-generation", t =>
                    {
                        t.Group(sharedGroup)
                            .WithHandler<GenerateThumbnailRequestHandler>()
                            .Instances(3);

                        //t.Group(sharedGroup)
                        //    .WithConsumer<GenerateThumbnailRequestSubscriber>()
                        //    .Instances(3);
                    });
                })
                //.WithDependencyResolverAsServiceLocator()
                .WithDependencyResolverAsAutofac()
                .WithSerializer(new JsonMessageSerializer())
                /*
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
                {
                    ConsumerConfigFactory = (group) => new Dictionary<string, object>
                    {
                        {KafkaConfigKeys.ConsumerKeys.AutoCommitEnableMs, 5000},
                        {KafkaConfigKeys.ConsumerKeys.StatisticsIntervalMs, 60000},
                        {
                            "default.topic.config", new Dictionary<string, object>
                            {
                                {KafkaConfigKeys.ConsumerKeys.AutoOffsetReset, KafkaConfigValues.AutoOffsetReset.Latest}
                            }
                        }
                    }
                })
                */
                .WithProviderRedis(new RedisMessageBusSettings(redisServer, redisSyncTimeout));

            var messageBus = messageBusBuilder.Build();
            return messageBus;
        }
    }
}