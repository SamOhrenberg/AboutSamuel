using PortfolioWebsite.Api.Mcp;

namespace PortfolioWebsite.Api.Tests.Unit.Mcp;

public class YearRangeTests
{
    private const int Now = 2026;

    [Theory]
    [InlineData("2019", "2022", 2019, 2022, false)]
    [InlineData("2021", null, 2021, Now, true)]
    [InlineData("2021", "", 2021, Now, true)]
    [InlineData("2021", "Present", 2021, Now, true)]
    [InlineData("2021", " current ", 2021, Now, true)]
    [InlineData("Spring 2020", "Fall 2021", 2020, 2021, false)]
    [InlineData("2020", "TBD", 2020, 2020, false)]
    [InlineData(null, "2018", 2018, 2018, false)]
    [InlineData(null, null, null, Now, true)]
    [InlineData("12345", "2020", 2020, 2020, false)]
    public void From_reads_free_text_years(string? start, string? end, int? expectedStart, int? expectedEnd, bool current)
    {
        var range = YearRange.From(start, end, Now);

        Assert.Equal(expectedStart, range.Start);
        Assert.Equal(expectedEnd, range.End);
        Assert.Equal(current, range.IsCurrent);
    }

    [Theory]
    [InlineData(2018, false)]
    [InlineData(2019, true)]
    [InlineData(2021, true)]
    [InlineData(2022, true)]
    [InlineData(2023, false)]
    public void Contains_is_inclusive(int year, bool expected) =>
        Assert.Equal(expected, YearRange.From("2019", "2022", Now).Contains(year));

    [Fact]
    public void Ongoing_range_contains_the_current_year() =>
        Assert.True(YearRange.From("2024", null, Now).Contains(Now));

    [Fact]
    public void Range_without_a_start_contains_nothing() =>
        Assert.False(YearRange.From(null, null, Now).Contains(Now));
}
