using FluentAssertions;
using PlikShare.Core.Utils;

namespace PlikShare.Tests;

public class IdsRangeTests
{
    [Fact]
    public void empty_sequence_returns_empty_list()
    {
        //given
        var numbers = Array.Empty<int>();
        //when
        var ranges = IdsRange.GroupConsecutiveIds(numbers);
        //then
        ranges.Ranges.Should().BeEmpty();
    }

    [Fact]
    public void single_number_returns_single_range()
    {
        //given
        var numbers = new[] { 1 };
        //when
        var ranges = IdsRange.GroupConsecutiveIds(numbers);
        //then
        ranges.Ranges.Should().BeEquivalentTo([new IdsRange(1, 1)]);
    }

    [Fact]
    public void consecutive_numbers_return_single_range()
    {
        //given
        var numbers = new[] { 1, 2, 3, 4, 5 };
        //when
        var ranges = IdsRange.GroupConsecutiveIds(numbers);
        //then
        ranges.Ranges.Should().BeEquivalentTo([new IdsRange(1, 5)]);
    }

    [Fact]
    public void non_consecutive_numbers_return_multiple_ranges()
    {
        //given
        var numbers = new[] { 1, 2, 3, 5, 6, 7, 10 };
        //when
        var ranges = IdsRange.GroupConsecutiveIds(numbers);
        //then
        ranges.Ranges.Should().BeEquivalentTo([
            new IdsRange(1, 3),
            new IdsRange(5, 7),
            new IdsRange(10, 10)
        ]);
    }

    [Fact]
    public void unsorted_input_is_handled_correctly()
    {
        //given
        var numbers = new[] { 5, 1, 3, 2, 4, 8, 6 };
        //when
        var ranges = IdsRange.GroupConsecutiveIds(numbers);
        //then
        ranges.Ranges.Should().BeEquivalentTo([
            new IdsRange(1, 6),
            new IdsRange(8, 8)
        ]);
    }

    [Fact]
    public void duplicate_numbers_are_handled_correctly()
    {
        //given
        var numbers = new[] { 1, 2, 2, 3, 3, 4 };
        //when
        var ranges = IdsRange.GroupConsecutiveIds(numbers);
        //then
        ranges.Ranges.Should().BeEquivalentTo([new IdsRange(1, 4)]);
    }

    [Fact]
    public void range_count_returns_correct_value()
    {
        //given
        var range = new IdsRange(5, 10);
        //when
        var count = range.Count();
        //then
        count.Should().Be(6);
    }

    [Fact]
    public void range_toString_formats_single_number_correctly()
    {
        //given
        var range = new IdsRange(5, 5);
        //when
        var str = range.ToString();
        //then
        str.Should().Be("5");
    }

    [Fact]
    public void range_toString_formats_range_correctly()
    {
        //given
        var range = new IdsRange(5, 10);
        //when
        var str = range.ToString();
        //then
        str.Should().Be("(5:10)");
    }
}