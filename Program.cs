global using Spectre.Console;

using DiscordMessageDataProcessor;
using Numerics.NET.Statistics;
using System.Runtime.CompilerServices;


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

while (true)
{
    List<Processor.IChannel> selectedChannels;

    string? chosenChannelKind = null;
    string? chosenServer = null;

    // Those variables are kept in the outer scope to survive the resets of the user's flow
    ColoringStrategy? coloringStrategy = null;
    MonthDrawingStrategy? monthDrawingStrategy = null;
    DrawingColor? drawingColor = null;
    bool? drawCompositionLayers = null;

FlowStart:
    // Selecting the server and channels
    {
        var selectedChannelKind = new SelectionPrompt<string>()
            .Title("Select what to analyze");

        const string optionDirectMessagesLabel = "Direct Messages";
        const string optionServerChannelsLabel = "Servers";

        var optionDirectMessages = selectedChannelKind.AddChoice(optionDirectMessagesLabel);
        var optionServerChannels = selectedChannelKind.AddChoice(optionServerChannelsLabel);

        chosenChannelKind ??= AnsiConsole.Prompt(selectedChannelKind);

        if (chosenChannelKind == optionServerChannelsLabel)
        {
            chosenServer ??= AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Select a server to process")
                    .PageSize(20)
                    .WrapAround()
                    .EnableSearch()
                    .AddChoices(processor.ChannelsByServerNames.Keys.OrderBy(x => x))
                );

            var serverChannels = processor.ChannelsByServerNames[chosenServer] ?? throw new Exception();
            processor.LoadChannelMetadata(fullFolderPath, serverChannels);

            var channelsGroupedByType = serverChannels
                .Select(c => (Channel: c, Metadata: processor.ExtraChannelMetadataById[c.Id]))
                .GroupBy(x => x.Metadata.Type);

            AnsiConsole.MarkupLineInterpolated($"Loaded the server");
            AnsiConsole.MarkupLineInterpolated($"    [teal]{"Channels (Total)",-20}[/] {serverChannels.Count}");
            CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.Text, "Text Channels");
            CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.Voice, "Voice Channels");
            CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.PrivateThread, "Private Threads");
            CountChannelsOfType(channelsGroupedByType, Processor.ChannelType.PublicThread, "Public Threads");

            var userActions = new SelectionPrompt<string>()
                .Title("Select an operation");

            const string optionAllChannelsLabel = "Analyze all channels";
            const string optionSelectedChannelsLabel = "Analyze selected channels";

            var optionAllChannels = userActions.AddChoice(optionAllChannelsLabel);
            var optionSelectedChannels = userActions.AddChoice(optionSelectedChannelsLabel);

            var chosenOption = AnsiConsole.Prompt(userActions);

            if (chosenOption == optionAllChannelsLabel)
            {
                selectedChannels = [.. serverChannels]; // Cloning the collection handles the casting
            }
            else if (chosenOption == optionSelectedChannelsLabel)
            {

                var multiSelectionOfChannels = new MultiSelectionPrompt<Processor.ChannelData>()
                    .Title("Select channels to analyze")
                    .PageSize(20)
                    .WrapAround()
                    .UseConverter(c => Markup.Escape(c.Name));

                AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.Text, "Text Channels");
                AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.Voice, "Voice Channels");
                AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.PrivateThread, "Private Threads");
                AddChoicesForChannelType(multiSelectionOfChannels, channelsGroupedByType, Processor.ChannelType.PublicThread, "Public Threads");

                selectedChannels = [.. AnsiConsole.Prompt(multiSelectionOfChannels)];
            }
            else
            {
                throw new Exception();
            }


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
        else if (chosenChannelKind == optionDirectMessagesLabel)
        {
            var selectedChannel = AnsiConsole.Prompt(new SelectionPrompt<Processor.IChannel>()
                .Title("Select a direct message to process")
                .WrapAround()
                .PageSize(20)
                .EnableSearch()
                .UseConverter(c => c.Name)
                .AddChoices(processor.DirectMessages.OrderBy(x => x.Username).ThenBy(x => x.Name).Cast<Processor.IChannel>())
            );

            selectedChannels = [selectedChannel];
        }
        else
        {
            throw new Exception();
        }

    }

    // Loading the message data
    {
        AnsiConsole.WriteLine("Loading following channels:");
        AnsiConsole.Write(new Columns(selectedChannels.Select(c => $"[teal]{Markup.Escape(c.Name)}[/]")));
        AnsiConsole.WriteLine();

        processor.LoadTimestamps(fullFolderPath, selectedChannels);
    }

FlowRenderHeatmap:
    // Displaying the heatmap
    {
        if (processor.MaxMessagesInADay > 0)
        {
            double[] allDayCounts = [.. processor.CountOfMessagesByDay.Values];

            var messageMedianCount = Stats.Median(allDayCounts);
            var messagesCountMaxInLog = Math.Log(processor.MaxMessagesInADay);

            {
                var coloringStrategyPrompt = new SelectionPrompt<ColoringStrategy>()
                    .Title("Select how the color ramp is calculated")
                    .UseConverter(s => s.Name);

                if (coloringStrategy != null)
                    coloringStrategyPrompt.AddChoice(new(Markup.Escape($"[as before]"), coloringStrategy.Conversion));

                coloringStrategyPrompt.AddChoice(new("Log (shows the general pattern)", x => Math.Log(x) / messagesCountMaxInLog));
                coloringStrategyPrompt.AddChoice(new("Median (shows the general pattern clamping the more active half)", x => x / messageMedianCount));
                coloringStrategyPrompt.AddChoice(new("Max (shows the peaks)", x => x / processor.MaxMessagesInADay));
                coloringStrategyPrompt.AddChoice(new("Binary (shows all non-zero days)", x => 1));

                coloringStrategy = AnsiConsole.Prompt(coloringStrategyPrompt);
            }

            {
                var monthDrawingStrategyPrompt = new SelectionPrompt<MonthDrawingStrategy>()
                    .Title("Select how the months are drawn")
                    .UseConverter(s => s.Name);

                if (monthDrawingStrategy != null)
                    monthDrawingStrategyPrompt.AddChoice(new(Markup.Escape($"[as before]"), monthDrawingStrategy.Mode));

                monthDrawingStrategyPrompt.AddChoice(new("Add lines in between", HeatmapRenderer.MonthRenderMode.Lines));
                monthDrawingStrategyPrompt.AddChoice(new("Separate months (increases width)", HeatmapRenderer.MonthRenderMode.Separation));
                monthDrawingStrategyPrompt.AddChoice(new("Alternating background", HeatmapRenderer.MonthRenderMode.Background));
                monthDrawingStrategyPrompt.AddChoice(new("None", HeatmapRenderer.MonthRenderMode.None));

                monthDrawingStrategy = AnsiConsole.Prompt(monthDrawingStrategyPrompt);
            }

            {
                var drawingColorPrompt = new SelectionPrompt<DrawingColor>()
                    .Title("Select how the months are drawn")
                    .UseConverter(s => s.Name);

                if (drawingColor != null)
                    drawingColorPrompt.AddChoice(new(Markup.Escape($"[as before]"), drawingColor.ColorHex));

                drawingColorPrompt.AddChoice(new("Blue (Discord)", "#5865F2"));
                drawingColorPrompt.AddChoice(new("Green (GitHub)", "#56D364"));
                drawingColorPrompt.AddChoice(new("White", "#FFFFFF"));

                drawingColor = AnsiConsole.Prompt(drawingColorPrompt);
            }

            {
                var drawCompositionLayersPrompt = new ConfirmationPrompt("Should the layers for compoosition be drawn? Use it only if you want to do further image processing on the data");
                drawCompositionLayersPrompt.DefaultValue = drawCompositionLayers ?? false;

                drawCompositionLayers = AnsiConsole.Prompt(drawCompositionLayersPrompt);
            }

            Func<DateTime, double> dataRenderingFunction = d =>
            {
                var count = processor.GetMessageCount(d);
                var alpha = coloringStrategy.Conversion(count);

                if (count == 0)
                    return 0;
                else
                    return Math.Max(alpha, 0.1f);
            };

            HeatmapRenderer.RenderHeatmap(processor.FirstMessageTimestamp, processor.LastMessageTimestamp, dataRenderingFunction, monthDrawingStrategy.Mode, drawingColor.ColorHex);

            var dataGrid = new Grid();

            dataGrid.AddColumn();
            dataGrid.AddColumn();
            dataGrid.AddColumn();


            dataGrid.AddRow(new Text("Max messages in a day", new Style(Color.Teal)),
                new Text(processor.MaxMessagesInADay.ToString()),
                new Text($"at {string.Join(", ", processor.CountOfMessagesByDay.Where(kvp => kvp.Value == processor.MaxMessagesInADay).Select(kvp => kvp.Key.ToString("dd.MM.yyyy")))}"));

            dataGrid.AddRow(new Text("Average messages in a day", new Style(Color.Teal)), new Text(processor.CountOfMessagesByDay.Values.Average().ToString("F1")));

            dataGrid.AddRow(new Text("Median messages in a day", new Style(Color.Teal)), new Text(messageMedianCount.ToString("F1")));

            dataGrid.AddRow(new Text("First Message", new Style(Color.Teal)), new Text(processor.FirstMessageTimestamp.ToString("dd.MM.yyyy HH:mm")));
            dataGrid.AddRow(new Text("Last Message", new Style(Color.Teal)), new Text(processor.LastMessageTimestamp.ToString("dd.MM.yyyy HH:mm")));

            AnsiConsole.Write(dataGrid);
            AnsiConsole.WriteLine();

            // The composition layers are used for more advanced image composition (overlappping multiple heatmaps and so on)
            if (drawCompositionLayers == true)
            {
                AnsiConsole.WriteLine("----------- Base Calendar -------------\n♦♦♦♦");
                HeatmapRenderer.RenderHeatmap(processor.FirstMessageTimestamp, processor.LastMessageTimestamp, 
                    d => 0, monthDrawingStrategy.Mode, drawingColor.ColorHex);

                AnsiConsole.WriteLine("------------- Data Only ---------------\n♦♦♦♦");
                HeatmapRenderer.RenderHeatmap(processor.FirstMessageTimestamp, processor.LastMessageTimestamp, 
                    dataRenderingFunction, HeatmapRenderer.MonthRenderMode.None, drawingColor.ColorHex, 
                    renderCalendar: false);

                AnsiConsole.WriteLine("------------ Data as Mask -------------\n♦♦♦♦");
                HeatmapRenderer.RenderHeatmap(processor.FirstMessageTimestamp, processor.LastMessageTimestamp,
                    dataRenderingFunction, HeatmapRenderer.MonthRenderMode.None, "#FFFFFF",
                    renderCalendar: false);

                AnsiConsole.WriteLine("--------- Data as Binary Mask ---------\n♦♦♦♦");
                HeatmapRenderer.RenderHeatmap(processor.FirstMessageTimestamp, processor.LastMessageTimestamp,
                    d => processor.GetMessageCount(d) != 0 ? 1.0 : 0.0, HeatmapRenderer.MonthRenderMode.None, "#FFFFFF",
                    renderCalendar: false);
            }
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"[orange3]Selected channels have no messages[/]");
        }
    }

    {
        const string optionReset = "Select a different direct message/server";
        const string optionDifferentChannels = "Select different channels";
        const string optionRerender = "Select different display method";
        const string optionQuit = "[[QUIT]]";

        var endOptionPrompt = new SelectionPrompt<string>()
            .Title("What do you want to do?");

        endOptionPrompt.AddChoice(optionRerender);

        if (selectedChannels.Count > 0 && selectedChannels[0] is not Processor.DirectMessagesData)
            endOptionPrompt.AddChoice(optionDifferentChannels);

        endOptionPrompt.AddChoice(optionReset);

        endOptionPrompt.AddChoice(optionQuit);

        var selectedEndOption = AnsiConsole.Prompt(endOptionPrompt);


        AnsiConsole.Clear();

        // Gotos aren't really a good option, but they were the easiest way to retrofit a "return to a point" flow into the code
        // Sorry
        switch (selectedEndOption)
        {
            case optionReset:
                {
                    chosenChannelKind = null;
                    chosenServer = null;
                    goto FlowStart;
                }
            case optionDifferentChannels:
                {
                    goto FlowStart;
                }
            case optionRerender: goto FlowRenderHeatmap;
            case optionQuit: return;
        }
    }
}

record ColoringStrategy(string Name, Func<int, double> Conversion);
record MonthDrawingStrategy(string Name, HeatmapRenderer.MonthRenderMode Mode);
record DrawingColor(string Name, string ColorHex);
