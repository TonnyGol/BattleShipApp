namespace BattleShipApp
{
    public class AppConfig
    {
        public required string MqttBrokerIp { get; set; }
        public required Dictionary<string, string> MqttTopics { get; set; }
        public required Dictionary<string, string> MqttMessages { get; set; }
        public required string ClientIdPrefix { get; set; }
        public required Dictionary<string, string> Scores { get; set; }
    }
}
