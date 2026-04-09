using SchoolScheduleApp.Core;
using System;
using Xunit;

namespace SchoolSchedule.Tests
{
    public class AcademicWeekHelperTests
    {
        [Fact]
        public void GetWeekRange_ReturnsMondayAndFriday_ForMiddleOfWeek()
        {
            var referenceDate = new DateTime(2026, 4, 8); // Wednesday

            var (monday, friday) = AcademicWeekHelper.GetWeekRange(0, referenceDate);

            Assert.Equal(new DateTime(2026, 4, 6), monday);
            Assert.Equal(new DateTime(2026, 4, 10), friday);
        }

        [Fact]
        public void GetWeekRange_AppliesWeekOffset()
        {
            var referenceDate = new DateTime(2026, 4, 8); // Wednesday

            var (monday, friday) = AcademicWeekHelper.GetWeekRange(1, referenceDate);

            Assert.Equal(new DateTime(2026, 4, 13), monday);
            Assert.Equal(new DateTime(2026, 4, 17), friday);
        }

        [Fact]
        public void GetWeekStartDate_ReturnsDateWithoutTime()
        {
            var referenceDate = new DateTime(2026, 4, 8, 15, 30, 10);

            var weekStart = AcademicWeekHelper.GetWeekStartDate(0, referenceDate);

            Assert.Equal(new DateTime(2026, 4, 6), weekStart);
            Assert.Equal(TimeSpan.Zero, weekStart.TimeOfDay);
        }

        [Fact]
        public void GetWeekStartKey_FormatsAsIsoDate()
        {
            var referenceDate = new DateTime(2026, 4, 8);

            var key = AcademicWeekHelper.GetWeekStartKey(0, referenceDate);

            Assert.Equal("2026-04-06", key);
        }

        [Fact]
        public void GetWeekText_UsesProvidedPrefix_AndContainsDateRange()
        {
            const string prefix = "Week: ";
            var text = AcademicWeekHelper.GetWeekText(0, prefix);

            Assert.StartsWith(prefix, text);
            Assert.Matches(@"^Week: \d{2}\.\d{2}\.\d{4} - \d{2}\.\d{2}\.\d{4}$", text);
        }
    }
}
