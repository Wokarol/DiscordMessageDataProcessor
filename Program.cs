global using Spectre.Console;

using DiscordMessageDataProcessor;


string folderPath = "discord-package";

string fullFolderPath = Path.GetFullPath(folderPath);

AnsiConsole.MarkupLineInterpolated($"Loading file at [blue]{fullFolderPath}[/]");

var processor = new Processor();

// Loading the data
{
    try
    {
        processor.LoadData(fullFolderPath);
    }
    catch (DirectoryNotFoundException e)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]Cannot find directory: {e.Message}[/]");
    }
    catch (FileNotFoundException e)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]Cannot find file: {e.Message}[/]");
    }
    catch (Exception e)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]Failed to load the data: {e.Message}[/]");
    }


    AnsiConsole.MarkupLineInterpolated($"Loaded the index");
    AnsiConsole.MarkupLineInterpolated($"    [teal]{"Servers",-20}[/] {processor.ChannelsByServerNames.Count}");
    AnsiConsole.MarkupLineInterpolated($"    [teal]{"Direct Messages",-20}[/] {processor.DirectMessages.Count}");
}


// Selecting the server and channels
{
    var selectedServer = AnsiConsole.Prompt(new SelectionPrompt<string>()
        .Title("Select a server to process")
        .PageSize(30)
        .AddChoices(processor.ChannelsByServerNames.Keys.OrderBy(x => x))
    );

    var serverChannels = processor.ChannelsByServerNames[selectedServer] ?? throw new Exception();
    processor.LoadChannelMetadata(folderPath, serverChannels);

    var channelsGroupedByType = serverChannels
        .Select(c => (Channel: c, Metadata: processor.ExtraChannelMetadataById[c.Id]))
        .GroupBy(x => x.Metadata.Type);

    AnsiConsole.MarkupLineInterpolated($"Loaded the server");
    AnsiConsole.MarkupLineInterpolated($"    [teal]{"Channels (Total)",-20}[/] {serverChannels.Count}");
    CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.Text, "Text Channels");
    CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.Voice, "Voice Channels");
    CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.PrivateThread, "Private Threads");
    CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.PublicThread, "Public Threads");

    var multiSelectionOfChannels = new MultiSelectionPrompt<Processor.ChannelData>()
        .Title("Select channels to analyze")
        .PageSize(30)
        .UseConverter(c => Markup.Escape(c.Name));

    AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.Text, "Text Channels");
    AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.Voice, "Voice Channels");
    AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.PrivateThread, "Private Threads");
    AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.PublicThread, "Public Threads");

    var selectedChannels = AnsiConsole.Prompt(multiSelectionOfChannels);


    static void AddChoicesForChannelType(MultiSelectionPrompt<Processor.ChannelData> multiSelectionOfChannels, IEnumerable<IGrouping<Processor.ChannelType, (Processor.ChannelData Channel, Processor.ExtraChannelMetadata Metadata)>> channelsGroupedByType, Processor.ChannelType typeToAdd, string labelToAdd)
    {
        var group = channelsGroupedByType.FirstOrDefault(g => g.Key == typeToAdd);
        if (group == null) return;

        multiSelectionOfChannels.AddChoiceGroup(new Processor.ChannelData(labelToAdd, ""), group.Select(x => x.Channel).OrderBy(c => c.Name));
    }

    static void CountChannelsOfType(IEnumerable<IGrouping<Processor.ChannelType, (Processor.ChannelData Channel, Processor.ExtraChannelMetadata Metadata)>> channelsGroupedByType, Processor.ChannelType typeToList, string labelToShow)
    {
        var group = channelsGroupedByType.FirstOrDefault(g => g.Key == typeToList);
        if (group == null) return;

        AnsiConsole.MarkupLineInterpolated($"        [teal]{labelToShow,-20}[/] {group.Count()}");
    }
}


