using System;
using System.Threading.Tasks;
using Common.Logging;
using Confluent.Kafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka
{
    /// <summary>
    /// Processor for incomming response messages in the request-response patterns. 
    /// See also <see cref="IKafkaTopicPartitionProcessor"/>.
    /// </summary>
    public class KafkaResponseProcessor : IKafkaTopicPartitionProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaResponseProcessor>();

        private readonly RequestResponseSettings _requestResponseSettings;
        private readonly MessageBusBase _messageBus;
        private readonly ICheckpointTrigger _checkpointTrigger;
        private readonly IKafkaCommitController _commitController;

        public KafkaResponseProcessor(RequestResponseSettings requestResponseSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, ICheckpointTrigger checkpointTrigger)
        {
            Log.InfoFormat("Creating for Group: {0}, Topic: {1}, Partition: {2}", requestResponseSettings.Group, requestResponseSettings.Topic, topicPartition);

            _requestResponseSettings = requestResponseSettings;
            TopicPartition = topicPartition;
            _commitController = commitController;
            _messageBus = messageBus;
            _checkpointTrigger = checkpointTrigger;
        }

        public KafkaResponseProcessor(RequestResponseSettings requestResponseSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus)
            : this(requestResponseSettings, topicPartition, commitController, messageBus, new CheckpointTrigger(requestResponseSettings))
        {
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
        }

        #endregion

        #region Implementation of IKafkaTopicPartitionProcessor

        public TopicPartition TopicPartition { get; }

        public async Task OnMessage(Message message)
        {
            try
            {
                await _messageBus.OnResponseArrived(message.Value, _requestResponseSettings.Topic);
            }
            catch (Exception e)
            {
                if (Log.IsErrorEnabled)
                    Log.ErrorFormat("Error occured while consuming response message: {0}", e, new MessageContextInfo(_requestResponseSettings.Group, message));

                // For response messages we can only continue and process all messages in the lease
                // ToDo: Add support for retry ?

                if (_requestResponseSettings.OnResponseMessageFault != null)
                {
                    // Call the hook
                    Log.TraceFormat("Executing the attached hook from {0}", nameof(_requestResponseSettings.OnResponseMessageFault));
                    try
                    {
                        _requestResponseSettings.OnResponseMessageFault(_requestResponseSettings, message, e);
                    }
                    catch (Exception e2)
                    {
                        Log.WarnFormat("Error handling hook failed for message: {0}", e2, new MessageContextInfo(_requestResponseSettings.Group, message));
                    }
                }
            }
            if (_checkpointTrigger.Increment())
            {
                await _commitController.Commit(message.TopicPartitionOffset);
            }
        }

        public async Task OnPartitionEndReached(TopicPartitionOffset offset)
        {
            await _commitController.Commit(offset);
        }

        public Task OnPartitionRevoked()
        {
            _checkpointTrigger.Reset();
            return Task.FromResult(0);
        }

        #endregion
    }
}