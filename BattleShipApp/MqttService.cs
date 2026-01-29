using MQTTnet;
using MQTTnet.Protocol;
using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BattleShipApp 
{
    public class MqttService
    {
        private readonly IMqttClient _mqttClient;

        private readonly MqttClientFactory _mqttFactory;

        public event Action<string, string>? MessageReceived;

        public MqttService()
        {
            _mqttFactory = new MqttClientFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();

            _mqttClient.ApplicationMessageReceivedAsync += HandleIncomingMessageInternal;
        }

        public async Task ConnectAsync(string brokerIp, string clientId)
        {
            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerIp)
                .WithClientId(clientId)
                .Build();

            if (!_mqttClient.IsConnected)
            {
                await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
            }
        }

        public async Task SubscribeAsync(string topic)
        {
            if (!_mqttClient.IsConnected) return;

            var subscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions, CancellationToken.None);
        }

        public async Task PublishAsync(string topic, string payload)
        {
            if (!_mqttClient.IsConnected) return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        private Task HandleIncomingMessageInternal(MqttApplicationMessageReceivedEventArgs e)
        {
            var payloadSequence = e.ApplicationMessage.Payload;
            if (!payloadSequence.IsEmpty)
            {
                string payload = Encoding.UTF8.GetString(payloadSequence.ToArray());
                string topic = e.ApplicationMessage.Topic;
                MessageReceived?.Invoke(payload, topic);
            }
            return Task.CompletedTask;
        }
    }
}