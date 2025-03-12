using FluentAssertions;
using PlikShare.Storages.Zip;

namespace PlikShare.Tests;

public class SteppingBufferTests
{
    [Fact]
    public void when_not_wrapped_returns_only_pushed_elements()
    {
        //given
        var buffer = new SteppingBuffer(5);
        //when
        buffer.Push(1);
        buffer.Push(2);
        buffer.Push(3);
        //then
        byte[] expected = [1, 2, 3];
        buffer.GetSpan().ToArray().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void when_filled_exactly_returns_all_elements()
    {
        //given
        var buffer = new SteppingBuffer(5);
        //when
        buffer.Push(1);
        buffer.Push(2);
        buffer.Push(3);
        buffer.Push(4);
        buffer.Push(5);
        //then
        byte[] expected = [1, 2, 3, 4, 5];
        buffer.GetSpan().ToArray().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void when_wrapped_once_returns_last_five_elements()
    {
        //given
        var buffer = new SteppingBuffer(5);

        //when
        buffer.Push(1);
        buffer.Push(2);
        buffer.Push(3);
        buffer.Push(4);
        buffer.Push(5);
        buffer.Push(6);
        buffer.Push(7);

        //then
        byte[] expected = [3, 4, 5, 6, 7];
        buffer.GetSpan().ToArray().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void when_wrapped_multiple_times_returns_last_five_elements()
    {
        //given
        var buffer = new SteppingBuffer(5);

        //when
        for (byte i = 1; i <= 12; i++)
            buffer.Push(i);

        //then
        byte[] expected = [8, 9, 10, 11, 12];
        buffer.GetSpan().ToArray().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void single_element_buffer_always_returns_last_element()
    {
        //given
        var buffer = new SteppingBuffer(1);
        //when
        buffer.Push(1);
        buffer.Push(2);
        buffer.Push(3);
        //then
        byte[] expected = [3];
        buffer.GetSpan().ToArray().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void empty_buffer_returns_empty_span()
    {
        //given
        var buffer = new SteppingBuffer(5);
        //when
        var span = buffer.GetSpan();
        //then
        span.Length.Should().Be(0);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, 3, new byte[] { 1, 2, 3 })]        // Not wrapped
    [InlineData(new byte[] { 1, 2, 3, 4 }, 3, new byte[] { 2, 3, 4 })]     // Wrapped once
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6 }, 3, new byte[] { 4, 5, 6 })] // Wrapped multiple times
    [InlineData(new byte[] { 1, 2, 3, 4, 5 }, 2, new byte[] { 4, 5 })]     // Size 2 buffer
    [InlineData(new byte[] { 1, 2, 3, 4, 5 }, 4, new byte[] { 2, 3, 4, 5 })] // Size 4 buffer
    public void always_returns_last_n_elements_after_wrapping(byte[] input, ushort size, byte[] expected)
    {
        //given
        var buffer = new SteppingBuffer(size);

        //when
        foreach (var value in input)
        {
            buffer.Push(value);
        }

        //then
        buffer.GetSpan().ToArray().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void can_handle_large_buffer()
    {
        //given
        const int size = 100;
        var buffer = new SteppingBuffer((ushort)size);
        var expected = new byte[size];

        //when
        for (byte i = 0; i < 150; i++)
        {
            buffer.Push(i);
            if (i >= 50)
            {
                expected[i - 50] = i;
            }
        }

        //then
        buffer.GetSpan().ToArray().Should().BeEquivalentTo(expected);
    }
}