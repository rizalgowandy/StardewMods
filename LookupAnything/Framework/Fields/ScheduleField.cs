using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Pathfinding;

namespace Pathoschild.Stardew.LookupAnything.Framework.Fields
{
    /// <summary>A metadata field which shows an NPC's schedule.</summary>
    /// <param name="schedule">The NPC's loaded schedule.</param>
    /// <param name="gameHelper">Provides utility methods for interacting with the game code.</param>
    internal class ScheduleField(Dictionary<int, SchedulePathDescription> schedule, GameHelper gameHelper) : GenericField(I18n.Npc_Schedule(), GetText(schedule, gameHelper))
    {
        /// <inheritdoc />
        public override Vector2? DrawValue(SpriteBatch spriteBatch, SpriteFont font, Vector2 position, float wrapWidth)
        {
            float topOffset = 0;

            foreach (IFormattedText text in this.Value)
            {
                topOffset += spriteBatch.DrawTextBlock(font, [text], new Vector2(position.X, position.Y + topOffset), wrapWidth).Y;
            }

            return new Vector2(wrapWidth, topOffset);
        }

        /// <summary>Get the text to display.</summary>
        /// <param name="schedule">An NPC's loaded schedule.</param>
        /// <param name="gameHelper">Provides utility methods for interacting with the game code.</param>
        private static IEnumerable<IFormattedText> GetText(Dictionary<int, SchedulePathDescription> schedule, GameHelper gameHelper)
        {
            var formattedSchedule = FormatSchedule(schedule).ToList();

            for (int i = 0; i < formattedSchedule.Count; i++)
            {
                (int time, SchedulePathDescription entry) = formattedSchedule[i];

                string timeString = formattedSchedule.Count == 1 ? I18n.Npc_Schedule_AllDay() : Game1.getTimeOfDayString(time);
                string locationDisplayName = gameHelper.GetLocationDisplayName(entry.targetLocationName, Game1.getLocationFromName(entry.targetLocationName).GetData());

                // check if the current game time is between current and next schedule entry
                bool isHappeningNow = Game1.timeOfDay >= time && (i >= formattedSchedule.Count - 1 || Game1.timeOfDay < formattedSchedule[i + 1].time);
                Color textColor = isHappeningNow ? Color.Black : Color.Gray;

                yield return new FormattedText($"{timeString} - {locationDisplayName}", textColor);
            }
        }

        /// <summary>Returns a collection of schedule entries sorted by time. Consecutive entries with the same target location are omitted.</summary>
        /// <param name="schedule">The schedule to format.</param>
        private static IEnumerable<(int time, SchedulePathDescription entry)> FormatSchedule(Dictionary<int, SchedulePathDescription> schedule)
        {
            List<int> sortedKeys = [.. schedule.Keys.OrderBy(key => key)];
            string prevTargetLocationName = string.Empty;

            foreach (int time in sortedKeys)
            {
                // skip if the entry does not exist or the previous entry was for the same location
                if (!schedule.TryGetValue(time, out SchedulePathDescription? entry) || entry.targetLocationName == prevTargetLocationName)
                    continue;

                prevTargetLocationName = entry.targetLocationName;
                yield return (time, entry);
            }
        }
    }
}
