using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace DiscordMessageDataProcessor;

public partial class Processor
{
    public Dictionary<string, List<ChannelData>> ChannelsByServerNames { get; } = [];
    public List<DirectMessagesData> DirectMessages { get; } = [];
    public Dictionary<string, ExtraChannelMetadata> ExtraChannelMetadataById { get; } = [];

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

        var channelIndexRaw = JsonSerializer.Deserialize<Dictionary<string, string>>(indexFile) ?? throw new Exception("Failed to load the channel index");

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

            var extraChannelDataRaw = JsonSerializer.Deserialize<ExtraChannelMetadataRaw>(channelDataFile) ?? throw new Exception("Failed to load the channel index");

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

    public record ChannelData(string Name, string Id);

    /// <param name="Username">If null, it's a group channel</param>
    public record DirectMessagesData(string Name, string? Username, string Id);
    public record ExtraChannelMetadataRaw(string id, string type);
    public record ExtraChannelMetadata(string Id, ChannelType Type);

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
