namespace Evently.Common.Infrastructure.Messaging;

public abstract class MessageBoxOptions
{
    public int LeaseSeconds { get; init; }

    public int RetentionDays { get; init; }

    public int PurgeIntervalMinutes { get; init; }

    public Dictionary<string, MessageLaneSettings> Lanes { get; init; } = [];

    public MessageLaneSettings LaneSettings(string lane)
    {
        MessageLaneSettings? laneSettings = Lanes.GetValueOrDefault(lane);

        return laneSettings ?? throw new InvalidOperationException($"No settings are configured for the '{lane}' lane");
    }

    public bool HasValidSettingsFor(IReadOnlyList<string> lanes) =>
        lanes.All(lane => Lanes.GetValueOrDefault(lane) is { } laneSettings && laneSettings.IsValid());
}
