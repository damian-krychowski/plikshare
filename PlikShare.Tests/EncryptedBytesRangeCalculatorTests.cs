using FluentAssertions;
using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using static PlikShare.Core.Encryption.EncryptedBytesRange;

namespace PlikShare.Tests;

public class EncryptedBytesRangeCalculatorTests
{
    // Small calculator for easy manual verification:
    //   headerSize=2, firstSegmentCiphertextSize=8, nextSegmentsCiphertextSize=10, tagSize=2
    //
    //   Segment 0 (first): [HEADER(2)] [CIPHERTEXT(8)] [TAG(2)] = 12 bytes total
    //   Segment 1:                     [CIPHERTEXT(10)] [TAG(2)] = 12 bytes total
    //   Segment 2:                     [CIPHERTEXT(10)] [TAG(2)] = 12 bytes total
    //
    //   Plaintext layout:
    //     Segment 0: plaintext bytes [0..7]
    //     Segment 1: plaintext bytes [8..17]
    //     Segment 2: plaintext bytes [18..27]
    //
    //   Encrypted layout:
    //     Segment 0: bytes [0..11]  (header[0..1], ciphertext[2..9], tag[10..11])
    //     Segment 1: bytes [12..23] (ciphertext[12..21], tag[22..23])
    //     Segment 2: bytes [24..35] (ciphertext[24..33], tag[34..35])

    private static EncryptedBytesRangeCalculator SmallCalculator() => new(
        headerSize: 2,
        firstSegmentCiphertextSize: 8,
        nextSegmentsCiphertextSize: 10,
        tagSize: 2);

    // Production calculator with real AES-256-GCM values
    private static EncryptedBytesRangeCalculator ProductionCalculator() =>
        Aes256GcmStreaming.EncryptedBytesRangeCalculator;

    #region FindSegment — small calculator

    [Fact]
    public void find_segment_for_index_in_first_segment()
    {
        //given
        var calculator = SmallCalculator();

        //when — encrypted index 5 is ciphertext inside segment 0
        var segment = calculator.FindSegment(
            encryptedIndex: 5,
            encryptedFileLastByteIndex: 35);

        //then — segment 0 starts at header offset (2), ends at 11
        segment.Number.Should().Be(0);
        segment.Start.Should().Be(2);
        segment.End.Should().Be(11);
    }

    [Fact]
    public void find_segment_for_index_at_last_byte_of_first_segment_ciphertext()
    {
        //given
        var calculator = SmallCalculator();

        //when — encrypted index 7 is last ciphertext byte of segment 0
        var segment = calculator.FindSegment(
            encryptedIndex: 7,
            encryptedFileLastByteIndex: 35);

        //then
        segment.Number.Should().Be(0);
        segment.Start.Should().Be(2);
        segment.End.Should().Be(11);
    }

    [Fact]
    public void find_segment_for_index_in_second_segment()
    {
        //given
        var calculator = SmallCalculator();

        //when — encrypted index 15 is inside segment 1 (starts at 12)
        var segment = calculator.FindSegment(
            encryptedIndex: 15,
            encryptedFileLastByteIndex: 35);

        //then
        segment.Number.Should().Be(1);
        segment.Start.Should().Be(12);
        segment.End.Should().Be(23);
    }

    [Fact]
    public void find_segment_for_index_in_third_segment()
    {
        //given
        var calculator = SmallCalculator();

        //when — encrypted index 25 is inside segment 2 (starts at 24)
        var segment = calculator.FindSegment(
            encryptedIndex: 25,
            encryptedFileLastByteIndex: 35);

        //then
        segment.Number.Should().Be(2);
        segment.Start.Should().Be(24);
        segment.End.Should().Be(35);
    }

    #endregion

    #region FromUnencryptedRange — small calculator

    [Fact]
    public void range_within_first_segment()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext range [2..5], file size 28 (3 segments)
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 2, End: 5),
            unencryptedFileSize: 28);

        //then — both in segment 0
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
        range.FirstSegmentReadOffset.Should().Be(2); // offset within segment 0 ciphertext
        range.LastSegmentReadOffset.Should().Be(5);
    }

    [Fact]
    public void range_within_second_segment()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext range [10..15], both in segment 1
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 10, End: 15),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(1);
        range.LastSegment.Number.Should().Be(1);
    }

    [Fact]
    public void range_crossing_first_and_second_segment_boundary()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext range [6..10]:
        //   byte 6 → segment 0 (plaintext [0..7])
        //   byte 10 → segment 1 (plaintext [8..17])
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 6, End: 10),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
        range.FirstSegmentReadOffset.Should().BeGreaterThanOrEqualTo(0);
        range.LastSegmentReadOffset.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void range_crossing_second_and_third_segment_boundary()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext range [15..20]:
        //   byte 15 → segment 1 (plaintext [8..17])
        //   byte 20 → segment 2 (plaintext [18..27])
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 15, End: 20),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(1);
        range.LastSegment.Number.Should().Be(2);
        range.FirstSegmentReadOffset.Should().BeGreaterThanOrEqualTo(0);
        range.LastSegmentReadOffset.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void range_spanning_all_three_segments()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext range [2..20], spans segments 0, 1, 2
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 2, End: 20),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(2);
    }

    [Fact]
    public void single_byte_range_in_first_segment()
    {
        //given
        var calculator = SmallCalculator();

        //when
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 3, End: 3),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
        range.FirstSegmentReadOffset.Should().Be(range.LastSegmentReadOffset);
    }

    [Fact]
    public void range_at_exact_segment_boundary_last_byte_of_first()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext byte 7 is last byte of segment 0
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 7, End: 7),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
    }

    [Fact]
    public void range_at_exact_segment_boundary_first_byte_of_second()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext byte 8 is first byte of segment 1
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 8, End: 8),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(1);
        range.LastSegment.Number.Should().Be(1);
        range.FirstSegmentReadOffset.Should().Be(0);
    }

    [Fact]
    public void range_crossing_boundary_at_exact_edge()
    {
        //given
        var calculator = SmallCalculator();

        //when — plaintext [7..8]: byte 7 is last of seg 0, byte 8 is first of seg 1
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 7, End: 8),
            unencryptedFileSize: 28);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
        range.LastSegmentReadOffset.Should().Be(0); // first byte of segment 1
    }

    #endregion

    #region Production constants — boundary tests

    [Fact]
    public void production_range_within_first_segment()
    {
        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024; // 2MB

        //when — first 100 bytes
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 0, End: 99),
            unencryptedFileSize: fileSize);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
        range.FirstSegmentReadOffset.Should().Be(0);
        range.LastSegmentReadOffset.Should().Be(99);
    }

    [Fact]
    public void production_range_near_end_of_first_segment()
    {
        // FirstSegmentCiphertextSize = 1,048,519
        // Plaintext byte 1,048,518 is the last byte of segment 0

        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        //when — last 100 bytes of segment 0
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_419, End: 1_048_518),
            unencryptedFileSize: fileSize);

        //then — should stay within segment 0
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
    }

    [Fact]
    public void production_range_crossing_first_segment_boundary()
    {
        // Plaintext byte 1,048,518 → segment 0 (last byte)
        // Plaintext byte 1,048,519 → segment 1 (first byte)

        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        //when — range [1,048,500 .. 1,048,600] crosses seg 0 → seg 1
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_500, End: 1_048_600),
            unencryptedFileSize: fileSize);

        //then
        range.FirstSegment.Number.Should().Be(0, "plaintext byte 1,048,500 is within segment 0 (max 1,048,518)");
        range.LastSegment.Number.Should().Be(1, "plaintext byte 1,048,600 is within segment 1 (starts at 1,048,519)");
        range.FirstSegmentReadOffset.Should().BeGreaterThanOrEqualTo(0, "offset within segment must be non-negative");
        range.LastSegmentReadOffset.Should().BeGreaterThanOrEqualTo(0, "offset within segment must be non-negative");
    }

    [Fact]
    public void production_range_at_exact_boundary()
    {
        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        //when — [1,048,518 .. 1,048,519] = last byte of seg 0, first byte of seg 1
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_518, End: 1_048_519),
            unencryptedFileSize: fileSize);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
        range.LastSegmentReadOffset.Should().Be(0, "byte 1,048,519 is the very first byte of segment 1");
    }

    [Fact]
    public void production_single_byte_at_segment_boundary()
    {
        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        //when — single byte at the last position of segment 0
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_518, End: 1_048_518),
            unencryptedFileSize: fileSize);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
    }

    [Fact]
    public void production_single_byte_at_start_of_second_segment()
    {
        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        //when
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_519, End: 1_048_519),
            unencryptedFileSize: fileSize);

        //then
        range.FirstSegment.Number.Should().Be(1);
        range.LastSegment.Number.Should().Be(1);
        range.FirstSegmentReadOffset.Should().Be(0);
    }

    [Fact]
    public void production_range_middle_of_second_segment()
    {
        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        //when — bytes entirely within segment 1
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_600, End: 1_048_700),
            unencryptedFileSize: fileSize);

        //then
        range.FirstSegment.Number.Should().Be(1);
        range.LastSegment.Number.Should().Be(1);
    }

    [Fact]
    public void production_offsets_are_consistent_with_range_length()
    {
        // For any range within a single segment, the offsets should produce
        // exactly the requested number of bytes.

        //given
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;
        long rangeStart = 100;
        long rangeEnd = 199;

        //when
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: rangeStart, End: rangeEnd),
            unencryptedFileSize: fileSize);

        //then — for single-segment range: LastSegmentReadOffset - FirstSegmentReadOffset + 1 = range length
        range.FirstSegment.Number.Should().Be(range.LastSegment.Number, "range should be within one segment");
        var readLength = range.LastSegmentReadOffset - range.FirstSegmentReadOffset + 1;
        readLength.Should().Be((int)(rangeEnd - rangeStart + 1));
    }

    [Fact]
    public void production_cross_boundary_offsets_produce_correct_total_length()
    {
        // For a 2-segment range, the total bytes read should equal the requested range length.
        // Segment 0 contributes: ciphertextSize - FirstSegmentReadOffset
        // Segment 1 contributes: LastSegmentReadOffset + 1

        //given
        var calculator = ProductionCalculator();
        var firstSegmentCiphertextSize = 1_048_519; // SegmentSize - TagSize - HeaderSize
        long fileSize = 2 * 1024 * 1024;
        long rangeStart = 1_048_500;
        long rangeEnd = 1_048_600;
        var expectedLength = rangeEnd - rangeStart + 1; // 101 bytes

        //when
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: rangeStart, End: rangeEnd),
            unencryptedFileSize: fileSize);

        //then
        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);

        var fromFirstSegment = firstSegmentCiphertextSize - range.FirstSegmentReadOffset;
        var fromLastSegment = range.LastSegmentReadOffset + 1;
        var totalBytes = fromFirstSegment + fromLastSegment;

        totalBytes.Should().Be((int)expectedLength,
            $"FirstSegmentReadOffset={range.FirstSegmentReadOffset}, LastSegmentReadOffset={range.LastSegmentReadOffset}");
    }

    #endregion

    #region FindSegment — guards and exact boundary validation

    // Small calculator encrypted layout:
    //   Segment 0: header[0..1], ciphertext[2..9], tag[10..11]
    //   Segment 1: ciphertext[12..21], tag[22..23]
    //   Segment 2: ciphertext[24..33], tag[34..35]

    [Fact]
    public void find_segment_throws_for_negative_index()
    {
        var calculator = SmallCalculator();

        var act = () => calculator.FindSegment(encryptedIndex: -1, encryptedFileLastByteIndex: 35);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_header()
    {
        var calculator = SmallCalculator();

        // Index 0 and 1 are header bytes
        var act0 = () => calculator.FindSegment(encryptedIndex: 0, encryptedFileLastByteIndex: 35);
        var act1 = () => calculator.FindSegment(encryptedIndex: 1, encryptedFileLastByteIndex: 35);

        act0.Should().Throw<ArgumentOutOfRangeException>();
        act1.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_beyond_file()
    {
        var calculator = SmallCalculator();

        var act = () => calculator.FindSegment(encryptedIndex: 36, encryptedFileLastByteIndex: 35);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_segment_0_tag()
    {
        var calculator = SmallCalculator();

        // Segment 0 tag is at bytes [10..11]
        var act10 = () => calculator.FindSegment(encryptedIndex: 10, encryptedFileLastByteIndex: 35);
        var act11 = () => calculator.FindSegment(encryptedIndex: 11, encryptedFileLastByteIndex: 35);

        act10.Should().Throw<ArgumentOutOfRangeException>();
        act11.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_segment_1_tag()
    {
        var calculator = SmallCalculator();

        // Segment 1 tag is at bytes [22..23]
        var act22 = () => calculator.FindSegment(encryptedIndex: 22, encryptedFileLastByteIndex: 35);
        var act23 = () => calculator.FindSegment(encryptedIndex: 23, encryptedFileLastByteIndex: 35);

        act22.Should().Throw<ArgumentOutOfRangeException>();
        act23.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_segment_2_tag()
    {
        var calculator = SmallCalculator();

        // Segment 2 tag is at bytes [34..35]
        var act34 = () => calculator.FindSegment(encryptedIndex: 34, encryptedFileLastByteIndex: 35);
        var act35 = () => calculator.FindSegment(encryptedIndex: 35, encryptedFileLastByteIndex: 35);

        act34.Should().Throw<ArgumentOutOfRangeException>();
        act35.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_0()
    {
        var calculator = SmallCalculator();

        // Byte 2 = first ciphertext byte of segment 0
        var segment = calculator.FindSegment(encryptedIndex: 2, encryptedFileLastByteIndex: 35);

        segment.Number.Should().Be(0);
    }

    [Fact]
    public void find_segment_last_ciphertext_byte_of_segment_0()
    {
        var calculator = SmallCalculator();

        // Byte 9 = last ciphertext byte of segment 0
        var segment = calculator.FindSegment(encryptedIndex: 9, encryptedFileLastByteIndex: 35);

        segment.Number.Should().Be(0);
    }

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_1()
    {
        var calculator = SmallCalculator();

        // Byte 12 = first ciphertext byte of segment 1
        var segment = calculator.FindSegment(encryptedIndex: 12, encryptedFileLastByteIndex: 35);

        segment.Number.Should().Be(1);
        segment.Start.Should().Be(12);
    }

    [Fact]
    public void find_segment_last_ciphertext_byte_of_segment_1()
    {
        var calculator = SmallCalculator();

        // Byte 21 = last ciphertext byte of segment 1
        var segment = calculator.FindSegment(encryptedIndex: 21, encryptedFileLastByteIndex: 35);

        segment.Number.Should().Be(1);
    }

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_2()
    {
        var calculator = SmallCalculator();

        // Byte 24 = first ciphertext byte of segment 2 (right after seg 1 tag)
        var segment = calculator.FindSegment(encryptedIndex: 24, encryptedFileLastByteIndex: 35);

        segment.Number.Should().Be(2);
        segment.Start.Should().Be(24);
    }

    [Fact]
    public void find_segment_last_ciphertext_byte_of_segment_2()
    {
        var calculator = SmallCalculator();

        // Byte 33 = last ciphertext byte of segment 2
        var segment = calculator.FindSegment(encryptedIndex: 33, encryptedFileLastByteIndex: 35);

        segment.Number.Should().Be(2);
    }

    #endregion
}
