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

        [Fact]
        public void Fmt_WithMilliseconds_AppendsThreeDigits()
        {
            Assert.Equal("00:00.000", AudioViewerControl.Fmt(TimeSpan.Zero, true));
            Assert.Equal("03:07.250", AudioViewerControl.Fmt(new TimeSpan(0, 0, 0, 3 * 60 + 7, 250), true));
            // Milliseconds are zero-padded, not truncated: 5 ms is ".005", not ".5".
            Assert.Equal("00:01.005", AudioViewerControl.Fmt(new TimeSpan(0, 0, 0, 1, 5), true));
        }

        [Fact]
        public void Fmt_WithMilliseconds_StillPromotesToHours()
        {
            Assert.Equal("2:05:09.750", AudioViewerControl.Fmt(new TimeSpan(0, 2, 5, 9, 750), true));
        }
    }
}
