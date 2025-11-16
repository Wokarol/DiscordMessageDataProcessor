using Wacton.Unicolour;

namespace DiscordMessageDataProcessor;
public static class HeatmapRenderer
{
    public static void RenderHeatmap(DateTime startDate, DateTime endDate, Func<DateTime, double> convertDateToValue)
    {
        var baseColor = new Unicolour("#151B23");
        var activeColor = new Unicolour("#56D364");

        var normalizedStartDate = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, 0);
        var normalizedEndDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 0, 0, 0, 0);

        var startYear = startDate.Year;
        var endYear = endDate.Year;

        if (endYear - startYear is < 0 or > 99)
        {
            throw new Exception($"Tried to render years from {startYear} to {endYear}. Are you sure it's correct?");
            return;
        }

        AnsiConsole.WriteLine();

        for (int year = startYear; year <= endYear; year++)
        {
            for (int row = 0; row < 7; row++)
            {
                if (row == 0)
                {
                    AnsiConsole.Write($"{year,-5}");
                }
                else
                {
                    AnsiConsole.Write($"{"",-5}");
                }

                for (int week = 0; week < 53; week++)
                {
                    var cellDate = DateFromCell(year, week, row);

                    if (cellDate == null)
                    {
                        AnsiConsole.Write("  ");
                        continue;
                    }


                    double alpha = convertDateToValue(cellDate.Value);
                    alpha = Math.Clamp(alpha, 0, 1);

                    var c = baseColor.Mix(activeColor, ColourSpace.Oklab, alpha);

                    var renderChar = '■';

                    //if (cellDate.Value < normalizedStartDate || cellDate.Value > normalizedEndDate)
                    //    renderChar = '□';

                    AnsiConsole.MarkupInterpolated($"[{c.Hex}]{renderChar} [/]");
                }

                AnsiConsole.WriteLine();
            }

            AnsiConsole.WriteLine();
        }
    }

    private static DateTime? DateFromCell(int year, int week, int row)
    {
        var firstDayOfTheYear = new DateTime(year, 1, 1, 0, 0, 0, 0, 0);
        var weekYearOffset = -((((int)firstDayOfTheYear.DayOfWeek - 1) + 7) % 7);
        if (weekYearOffset == -7) weekYearOffset = 0;

        var date = firstDayOfTheYear.AddDays(week * 7 + row + weekYearOffset);

        if (date.Year != year)
            return null;

        return date;
    }
}
