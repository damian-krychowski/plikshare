using FluentAssertions;
using PlikShare.Storages.FileReading;

namespace PlikShare.Tests;

public class RangedReadOnlyStreamTests
{
    private static MemoryStream CreateSourceStream(int size)
    {
        var data = new byte[size];
        for (var i = 0; i < size; i++)
            data[i] = (byte)(i % 256);
        return new MemoryStream(data, writable: false);
    }

    // ---------- Construction ----------

    [Fact]
    public void constructor_throws_when_inner_stream_is_null()
    {
        //given
        Stream inner = null!;

        //when
        var act = () => new RangedReadOnlyStream(inner, start: 0, length: 10);

        //then
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void constructor_throws_when_start_is_negative()
    {
        //given
        var inner = CreateSourceStream(100);

        //when
        var act = () => new RangedReadOnlyStream(inner, start: -1, length: 10);

        //then
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void constructor_throws_when_length_is_negative()
    {
        //given
        var inner = CreateSourceStream(100);

        //when
        var act = () => new RangedReadOnlyStream(inner, start: 0, length: -1);

        //then
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void constructor_throws_when_inner_stream_is_not_readable()
    {
        //given
        var inner = new NonReadableStream();

        //when
        var act = () => new RangedReadOnlyStream(inner, start: 0, length: 10);

        //then
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void constructor_throws_when_inner_stream_is_not_seekable()
    {
        //given
        var inner = new NonSeekableStream();

        //when
        var act = () => new RangedReadOnlyStream(inner, start: 0, length: 10);

        //then
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void constructor_seeks_inner_stream_to_start_position()
    {
        //given
        var inner = CreateSourceStream(100);

        //when
        _ = new RangedReadOnlyStream(inner, start: 25, length: 10);

        //then
        inner.Position.Should().Be(25);
    }

    [Fact]
    public void constructor_accepts_zero_length()
    {
        //given
        var inner = CreateSourceStream(100);

        //when
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 0);

        //then
        stream.Length.Should().Be(0);
        stream.Position.Should().Be(0);
    }

    [Fact]
    public void constructor_accepts_start_beyond_inner_stream_length()
    {
        //given
        var inner = CreateSourceStream(10);

        //when
        var act = () => new RangedReadOnlyStream(inner, start: 100, length: 5);

        //then
        // MemoryStream allows seeking beyond length; wrapper mirrors underlying stream semantics
        act.Should().NotThrow();
    }

    // ---------- Capability flags ----------

    [Fact]
    public void stream_is_readable_not_seekable_not_writable()
    {
        //given
        var inner = CreateSourceStream(100);

        //when
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);

        //then
        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeFalse();
        stream.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void length_reflects_window_length_not_inner_stream_length()
    {
        //given
        var inner = CreateSourceStream(1000);

        //when
        var stream = new RangedReadOnlyStream(inner, start: 100, length: 42);

        //then
        stream.Length.Should().Be(42);
    }

    [Fact]
    public void position_starts_at_zero()
    {
        //given
        var inner = CreateSourceStream(100);

        //when
        var stream = new RangedReadOnlyStream(inner, start: 50, length: 10);

        //then
        stream.Position.Should().Be(0);
    }

    [Fact]
    public void setting_position_throws()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);

        //when
        var act = () => stream.Position = 5;

        //then
        act.Should().Throw<NotSupportedException>();
    }

    // ---------- Sync reads ----------

    [Fact]
    public void read_returns_correct_bytes_from_start_of_range()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 5);
        var buffer = new byte[5];

        //when
        var read = stream.Read(buffer, 0, 5);

        //then
        read.Should().Be(5);
        buffer.Should().Equal(10, 11, 12, 13, 14);
    }

    [Fact]
    public void read_does_not_cross_range_boundary()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 5);
        var buffer = new byte[100];

        //when
        var read = stream.Read(buffer, 0, 100);

        //then
        read.Should().Be(5);
        buffer.Take(5).Should().Equal(10, 11, 12, 13, 14);
    }

    [Fact]
    public void read_returns_zero_after_range_is_exhausted()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 5);
        var buffer = new byte[5];
        stream.Read(buffer, 0, 5);

        //when
        var read = stream.Read(buffer, 0, 5);

        //then
        read.Should().Be(0);
    }

    [Fact]
    public void multiple_small_reads_yield_full_range_in_order()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 20, length: 10);
        var collected = new List<byte>();
        var buffer = new byte[3];

        //when
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            collected.AddRange(buffer.Take(read));

        //then
        collected.Should().Equal(20, 21, 22, 23, 24, 25, 26, 27, 28, 29);
    }

    [Fact]
    public void position_advances_by_bytes_read()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 20);
        var buffer = new byte[7];

        //when
        stream.Read(buffer, 0, 7);

        //then
        stream.Position.Should().Be(7);
    }

    [Fact]
    public void position_equals_length_after_exhaustion()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);
        var buffer = new byte[10];

        //when
        stream.Read(buffer, 0, 10);

        //then
        stream.Position.Should().Be(10);
    }

    [Fact]
    public void read_with_zero_length_window_returns_zero_immediately()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 0);
        var buffer = new byte[10];

        //when
        var read = stream.Read(buffer, 0, 10);

        //then
        read.Should().Be(0);
        stream.Position.Should().Be(0);
    }

    [Fact]
    public void read_respects_offset_and_count_in_buffer()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);
        var buffer = new byte[20];
        Array.Fill(buffer, (byte)0xFF);

        //when
        var read = stream.Read(buffer, offset: 5, count: 3);

        //then
        read.Should().Be(3);
        buffer[0..5].Should().AllBeEquivalentTo((byte)0xFF);
        buffer[5..8].Should().Equal(0, 1, 2);
        buffer[8..].Should().AllBeEquivalentTo((byte)0xFF);
    }

    [Fact]
    public void read_span_overload_returns_correct_bytes()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 30, length: 4);
        Span<byte> buffer = stackalloc byte[4];

        //when
        var read = stream.Read(buffer);

        //then
        read.Should().Be(4);
        buffer.ToArray().Should().Equal(30, 31, 32, 33);
    }

    // ---------- Async reads ----------

    [Fact]
    public async Task read_async_returns_correct_bytes_from_start_of_range()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 5);
        var buffer = new byte[5];

        //when
        var read = await stream.ReadAsync(buffer.AsMemory(0, 5));

        //then
        read.Should().Be(5);
        buffer.Should().Equal(10, 11, 12, 13, 14);
    }

    [Fact]
    public async Task read_async_does_not_cross_range_boundary()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 5);
        var buffer = new byte[100];

        //when
        var read = await stream.ReadAsync(buffer.AsMemory(0, 100));

        //then
        read.Should().Be(5);
        buffer.Take(5).Should().Equal(10, 11, 12, 13, 14);
    }

    [Fact]
    public async Task read_async_returns_zero_after_range_is_exhausted()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 5);
        var buffer = new byte[5];
        await stream.ReadAsync(buffer.AsMemory(0, 5));

        //when
        var read = await stream.ReadAsync(buffer.AsMemory(0, 5));

        //then
        read.Should().Be(0);
    }

    [Fact]
    public async Task read_async_legacy_overload_works()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 50, length: 4);
        var buffer = new byte[4];

        //when
        var read = await stream.ReadAsync(buffer, 0, 4);

        //then
        read.Should().Be(4);
        buffer.Should().Equal(50, 51, 52, 53);
    }

    [Fact]
    public async Task read_async_honors_cancellation()
    {
        //given
        var inner = new SlowStream(CreateSourceStream(100));
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);
        var buffer = new byte[10];
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        //when
        var act = async () => await stream.ReadAsync(buffer.AsMemory(), cts.Token);

        //then
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task copy_to_async_copies_only_range_contents()
    {
        //given
        var inner = CreateSourceStream(1000);
        var stream = new RangedReadOnlyStream(inner, start: 100, length: 50);
        var target = new MemoryStream();

        //when
        await stream.CopyToAsync(target);

        //then
        target.Length.Should().Be(50);
        target.ToArray().Should().Equal(Enumerable.Range(100, 50).Select(i => (byte)(i % 256)));
    }

    // ---------- Full-range and edge ranges ----------

    [Fact]
    public void reading_full_inner_stream_yields_all_bytes()
    {
        //given
        var inner = CreateSourceStream(256);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 256);
        var buffer = new byte[256];

        //when
        var read = stream.Read(buffer, 0, 256);

        //then
        read.Should().Be(256);
        buffer.Should().Equal(Enumerable.Range(0, 256).Select(i => (byte)i));
    }

    [Fact]
    public void range_at_very_end_of_inner_stream_reads_correctly()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 95, length: 5);
        var buffer = new byte[10];

        //when
        var read = stream.Read(buffer, 0, 10);

        //then
        read.Should().Be(5);
        buffer.Take(5).Should().Equal(95, 96, 97, 98, 99);
    }

    [Fact]
    public void read_stops_at_declared_length_even_if_inner_has_more_bytes()
    {
        //given
        var inner = CreateSourceStream(1000);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);
        var buffer = new byte[1000];

        //when
        var firstRead = stream.Read(buffer, 0, 1000);
        var secondRead = stream.Read(buffer, 0, 1000);

        //then
        firstRead.Should().Be(10);
        secondRead.Should().Be(0);
    }

    [Fact]
    public void read_returns_fewer_bytes_when_inner_stream_ends_early()
    {
        //given
        // Declare length=50 but inner only has 20 bytes remaining after start.
        var inner = CreateSourceStream(30);
        var stream = new RangedReadOnlyStream(inner, start: 10, length: 50);
        var buffer = new byte[100];

        //when
        var first = stream.Read(buffer, 0, 100);
        var second = stream.Read(buffer, 0, 100);

        //then
        first.Should().Be(20);
        second.Should().Be(0); // inner exhausted; wrapper faithfully reports EOF
    }

    // ---------- Unsupported operations ----------

    [Fact]
    public void seek_throws_not_supported()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);

        //when
        var act = () => stream.Seek(0, SeekOrigin.Begin);

        //then
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void set_length_throws_not_supported()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);

        //when
        var act = () => stream.SetLength(5);

        //then
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void write_throws_not_supported()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);
        var buffer = new byte[5];

        //when
        var act = () => stream.Write(buffer, 0, 5);

        //then
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void flush_does_not_throw()
    {
        //given
        var inner = CreateSourceStream(100);
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);

        //when
        var act = () => stream.Flush();

        //then
        act.Should().NotThrow();
    }

    // ---------- Dispose ----------

    [Fact]
    public void dispose_disposes_inner_stream_by_default()
    {
        //given
        var inner = new DisposeTrackingStream(CreateSourceStream(100));
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);

        //when
        stream.Dispose();

        //then
        inner.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void dispose_does_not_dispose_inner_stream_when_leave_open_is_true()
    {
        //given
        var inner = new DisposeTrackingStream(CreateSourceStream(100));
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10, leaveOpen: true);

        //when
        stream.Dispose();

        //then
        inner.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task dispose_async_disposes_inner_stream_by_default()
    {
        //given
        var inner = new DisposeTrackingStream(CreateSourceStream(100));
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10);

        //when
        await stream.DisposeAsync();

        //then
        inner.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task dispose_async_does_not_dispose_inner_stream_when_leave_open_is_true()
    {
        //given
        var inner = new DisposeTrackingStream(CreateSourceStream(100));
        var stream = new RangedReadOnlyStream(inner, start: 0, length: 10, leaveOpen: true);

        //when
        await stream.DisposeAsync();

        //then
        inner.IsDisposed.Should().BeFalse();
    }

    // ---------- Test helpers ----------

    private sealed class NonReadableStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    private sealed class NonSeekableStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DisposeTrackingStream(Stream inner) : Stream
    {
        public bool IsDisposed { get; private set; }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing) IsDisposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return base.DisposeAsync();
        }
    }

    private sealed class SlowStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(500, cancellationToken);
            return await inner.ReadAsync(buffer, cancellationToken);
        }
    }
}