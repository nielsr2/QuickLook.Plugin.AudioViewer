using System;
using Xunit;

namespace QuickLook.Plugin.AudioViewer.Tests
{
    public class TimeFormatTests
    {
        [Fact]
        public void Fmt_UnderOneHour_IsMinutesSeconds()
        {
            Assert.Equal("03:07", AudioViewerControl.Fmt(new TimeSpan(0, 3, 7)));
            Assert.Equal("00:00", AudioViewerControl.Fmt(TimeSpan.Zero));
            Assert.Equal("59:59", AudioViewerControl.Fmt(new TimeSpan(0, 59, 59)));
        }

        [Fact]
        public void Fmt_OneHourOrMore_IncludesHours()
        {
            Assert.Equal("1:00:00", AudioViewerControl.Fmt(new TimeSpan(1, 0, 0)));
            Assert.Equal("2:05:09", AudioViewerControl.Fmt(new TimeSpan(2, 5, 9)));
        }
    }
}
