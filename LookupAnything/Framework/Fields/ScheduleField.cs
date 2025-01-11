using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

namespace Pathoschild.Stardew.LookupAnything.Framework.Fields;

/// <summary>A metadata field which shows an NPC's schedule.</summary>
/// <param name="schedule">The NPC's loaded schedule.</param>
/// <param name="gameHelper">Provides utility methods for interacting with the game code.</param>
internal class ScheduleField(Dictionary<int, SchedulePathDescription> schedule, GameHelper gameHelper) : GenericField(I18n.Npc_Schedule(), GetText(schedule, gameHelper))
{
    /*********
    ** Private methods
    *********/
    /// <summary>Get the text to display.</summary>
    /// <param name="schedule">An NPC's loaded schedule.</param>
    /// <param name="gameHelper">Provides utility methods for interacting with the game code.</param>
    private static IEnumerable<IFormattedText> GetText(Dictionary<int, SchedulePathDescription> schedule, GameHelper gameHelper)
    {
        List<ScheduleEntry> formattedSchedule = FormatSchedule(schedule).ToList();

        for (int i = 0; i < formattedSchedule.Count; i++)
        {
            (int time, SchedulePathDescription entry) = formattedSchedule[i];

            string locationName = gameHelper.GetLocationDisplayName(entry.targetLocationName, Game1.getLocationFromName(entry.targetLocationName).GetData());
            bool isStarted = Game1.timeOfDay >= time;
            bool isFinished = i < formattedSchedule.Count - 1 && Game1.timeOfDay >= formattedSchedule[i + 1].Time;

            Color textColor = isStarted
                ? (isFinished ? Color.Gray : Color.Green)
                : Color.Black;

            if (i > 0)
                yield return new FormattedText(Environment.NewLine);
            yield return new FormattedText($"{Game1.getTimeOfDayString(time)} - {locationName}", textColor);
        }
    }

    /// <summary>Returns a collection of schedule entries sorted by time. Consecutive entries with the same target location are omitted.</summary>
    /// <param name="schedule">The schedule to format.</param>
    private static IEnumerable<ScheduleEntry> FormatSchedule(Dictionary<int, SchedulePathDescription> schedule)
    {
        List<int> sortedKeys = [.. schedule.Keys.OrderBy(key => key)];
        string prevTargetLocationName = string.Empty;

        foreach (int time in sortedKeys)
        {
            // skip if the entry does not exist or the previous entry was for the same location
            if (!schedule.TryGetValue(time, out SchedulePathDescription? entry) || entry.targetLocationName == prevTargetLocationName)
                continue;

            prevTargetLocationName = entry.targetLocationName;
            yield return new ScheduleEntry(time, entry);
        }
    }

    /// <summary>An entry in an NPC's schedule.</summary>
    /// <param name="Time">The time that the event starts.</param>
    /// <param name="Description">A description of the event.</param>
    private record ScheduleEntry(int Time, SchedulePathDescription Description);
}
