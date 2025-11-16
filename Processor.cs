using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using static DiscordMessageDataProcessor.Processor;

namespace DiscordMessageDataProcessor;

public partial class Processor
{
    private JsonSerializerOptions options;

    public Processor()
    {
        options = new()
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        };
    }

    public Dictionary<string, List<ChannelData>> ChannelsByServerNames { get; } = [];
    public List<DirectMessagesData> DirectMessages { get; } = [];
    public Dictionary<string, ExtraChannelMetadata> ExtraChannelMetadataById { get; } = [];

    public Dictionary<DateTime, int> CountOfMessagesByDay { get; } = [];
    public int MaxMessagesInADay { get; private set; }
    public DateTime FirstMessageTimestamp { get; private set; }
    public DateTime LastMessageTimestamp { get; private set; }


    public void LoadData(string folderPath)
    {
        string messagesFolderPath = Path.Combine(folderPath, "Messages");
        string channelsIndexPath = Path.Combine(messagesFolderPath, "index.json");

        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException(folderPath);

        if (!Directory.Exists(messagesFolderPath))
            throw new DirectoryNotFoundException(messagesFolderPath);

        if (!File.Exists(channelsIndexPath))
            throw new FileNotFoundException(channelsIndexPath);

        using var indexFile = File.OpenRead(channelsIndexPath);

        var channelIndexRaw = JsonSerializer.Deserialize<Dictionary<string, string>>(indexFile, options) ?? throw new Exception("Failed to load the channel index");

        ChannelsByServerNames.Clear();
        DirectMessages.Clear();

        foreach (var (id, fullChannelName) in channelIndexRaw)
        {
            if (ChannelRegex().IsMatch(fullChannelName))
            {
                // The channel is a channel in the server
                var match = ChannelRegex().Match(fullChannelName);
                var channelName = match.Groups["Name"].Value;
                var channelServer = match.Groups["Server"].Value;

                if (channelName == "Unknown channel")
                    continue;

                if (!ChannelsByServerNames.TryGetValue(channelServer, out var channels))
                {
                    channels = new List<ChannelData>();
                    ChannelsByServerNames[channelServer] = channels;
                }

                channels.Add(new(channelName, id));
            }
            else if (DirectMessageRegex().IsMatch(fullChannelName))
            {
                // The channel is a direct message
                var match = DirectMessageRegex().Match(fullChannelName);
                var username = match.Groups["User"].Value;

                DirectMessages.Add(new(fullChannelName, username, id));
            }
            else if(fullChannelName == "Direct Message with Unknown Participant" || fullChannelName == "None")
            {
                // We don't care about that
            }
            else
            {
                // A group chat... probably
                DirectMessages.Add(new(fullChannelName, null, id));
            }
        }
    }

    public void LoadChannelMetadata(string folderPath, List<ChannelData> channelsToLoad)
    {
        string messagesFolderPath = Path.Combine(folderPath, "Messages");

        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException(folderPath);

        if (!Directory.Exists(messagesFolderPath))
            throw new DirectoryNotFoundException(messagesFolderPath);

        foreach (var channelData in channelsToLoad)
        {
            string channelFolderPath = Path.Combine(messagesFolderPath, $"c{channelData.Id}");
            string channelDataPath = Path.Combine(channelFolderPath, $"channel.json");

            if (!Directory.Exists(channelFolderPath))
                throw new DirectoryNotFoundException(channelFolderPath);

            if (!File.Exists(channelDataPath))
                throw new FileNotFoundException(channelDataPath);

            using var channelDataFile = File.OpenRead(channelDataPath);

            var extraChannelDataRaw = JsonSerializer.Deserialize(channelDataFile, SourceGenerationContext.Default.ExtraChannelMetadataRaw) ?? throw new Exception("Failed to load the channel metadata");

            var type = extraChannelDataRaw.type switch
            {
                "PRIVATE_THREAD" => ChannelType.PrivateThread,
                "PUBLIC_THREAD" => ChannelType.PublicThread,
                "GUILD_TEXT" => ChannelType.Text,
                "GUILD_VOICE" => ChannelType.Voice,
                _ => ChannelType.Invalid,
            };

            ExtraChannelMetadataById[channelData.Id] = new ExtraChannelMetadata(channelData.Id, type);
        }
    }

    public void LoadTimestamps(string folderPath, List<IChannel> selectedChannels)
    {
        CountOfMessagesByDay.Clear();
        MaxMessagesInADay = 0;
        FirstMessageTimestamp = new DateTime(9999, 1, 1, 0, 0, 0, 0);
        LastMessageTimestamp = new DateTime(1111, 1, 1, 0, 0, 0, 0);

        string messagesFolderPath = Path.Combine(folderPath, "Messages");

        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException(folderPath);

        if (!Directory.Exists(messagesFolderPath))
            throw new DirectoryNotFoundException(messagesFolderPath);

        foreach (var channel in selectedChannels)
        {
            string channelFolderPath = Path.Combine(messagesFolderPath, $"c{channel.Id}");
            string channelMessagesPath = Path.Combine(channelFolderPath, $"messages.json");

            if (!Directory.Exists(channelFolderPath))
                throw new DirectoryNotFoundException(channelFolderPath);

            if (!File.Exists(channelMessagesPath))
                throw new FileNotFoundException(channelMessagesPath);

            using var channelMessagesFile = File.OpenRead(channelMessagesPath);
            var messagesDataRaw = JsonSerializer.Deserialize(channelMessagesFile, SourceGenerationContext.Default.ListMessageDataRaw) ?? throw new Exception("Failed to load the channel messages");

            foreach (var message in messagesDataRaw)
            {
                var date = DateTime.Parse(message.Timestamp);
                var normalizedDate = NormalizeDate(date);

                if (CountOfMessagesByDay.TryGetValue(normalizedDate, out int current))
                {
                    CountOfMessagesByDay[normalizedDate] = current + 1;
                }
                else
                {
                    CountOfMessagesByDay[normalizedDate] = 1;
                }


                if (FirstMessageTimestamp > date) FirstMessageTimestamp = date;
                if (LastMessageTimestamp < date) LastMessageTimestamp = date;
            }

            if (CountOfMessagesByDay.Count > 0)
            {
                MaxMessagesInADay = CountOfMessagesByDay.Values.Max();
            }
        }
    }

    public int GetMessageCount(DateTime date)
    {

        if (CountOfMessagesByDay.TryGetValue(NormalizeDate(date), out var count))
        {
            return count;
        }
        else
        {
            return 0;
        }
    }

    public DateTime NormalizeDate(DateTime date) => new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);

    public record ChannelData(string Name, string Id) : IChannel;

    /// <param name="Username">If null, it's a group channel</param>
    public record DirectMessagesData(string Name, string? Username, string Id) : IChannel;
    public record ExtraChannelMetadata(string Id, ChannelType Type);

    public class ExtraChannelMetadataRaw
    {
        public string id { get; set; }
        public string type { get; set; }
    }

    public class MessageDataRaw
    {
        public string Timestamp { get; set; }
    }

    public interface IChannel
    {
        string Name { get; }
        string Id { get; }
    }

    public enum ChannelType
    {
        Invalid,
        PrivateThread,
        PublicThread,
        Text,
        Voice,
    }


    [GeneratedRegex(@"^(?<Name>.+)\sin\s(?<Server>.+)$")]
    private static partial Regex ChannelRegex();

    [GeneratedRegex(@"^Direct Message with (?<User>.+?)#\d+$")]
    private static partial Regex DirectMessageRegex();
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ExtraChannelMetadataRaw))]
[JsonSerializable(typeof(List<MessageDataRaw>))]
internal partial class SourceGenerationContext : JsonSerializerContext { }