using FluentAssertions;
using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks.RangeRequests;

namespace PlikShare.Tests;

public class EncryptedBytesRangeCalculatorV2Tests
{
    // Small calculator for easy manual verification:
    //   segmentSize=12, tagSize=2, baseHeaderSize=2, stepSaltSize=1
    //   → nextSegmentsCiphertextSize = 12 - 2 = 10
    //
    // chainStepsCount=0: header=2, firstSegmentCiphertextSize=8
    //   Segment 0: [HEADER(2)] [CIPHERTEXT(8)] [TAG(2)] = bytes [0..11]
    //   Segment 1:              [CIPHERTEXT(10)] [TAG(2)] = bytes [12..23]
    //   Segment 2:              [CIPHERTEXT(10)] [TAG(2)] = bytes [24..35]
    //   Plaintext: seg 0 = [0..7], seg 1 = [8..17], seg 2 = [18..27]; fileSize=28
    //
    // chainStepsCount=1: header=3, firstSegmentCiphertextSize=7
    //   Segment 0: [HEADER(3)] [CIPHERTEXT(7)] [TAG(2)] = bytes [0..11]
    //   Segment 1:              [CIPHERTEXT(10)] [TAG(2)] = bytes [12..23]
    //   Segment 2:              [CIPHERTEXT(10)] [TAG(2)] = bytes [24..35]
    //   Plaintext: seg 0 = [0..6], seg 1 = [7..16], seg 2 = [17..26]; fileSize=27
    //
    // chainStepsCount=2: header=4, firstSegmentCiphertextSize=6
    //   Segment 0: [HEADER(4)] [CIPHERTEXT(6)] [TAG(2)] = bytes [0..11]
    //   Plaintext: seg 0 = [0..5], seg 1 = [6..15], seg 2 = [16..25]; fileSize=26

    private static EncryptedBytesRangeCalculatorV2 SmallCalculator() => new(
        segmentSize: 12,
        tagSize: 2,
        baseHeaderSize: 2,
        stepSaltSize: 1);

    private static EncryptedBytesRangeCalculatorV2 ProductionCalculator() =>
        Aes256GcmStreamingV2.EncryptedBytesRangeCalculator;

    #region GetHeaderSize

    [Fact]
    public void header_size_with_zero_chain_steps_equals_base()
    {
        var calculator = SmallCalculator();

        calculator.GetHeaderSize(0).Should().Be(2);
    }

    [Fact]
    public void header_size_with_one_chain_step_adds_one_step_salt()
    {
        var calculator = SmallCalculator();

        calculator.GetHeaderSize(1).Should().Be(3);
    }

    [Fact]
    public void header_size_with_four_chain_steps_adds_four_step_salts()
    {
        var calculator = SmallCalculator();

        calculator.GetHeaderSize(4).Should().Be(6);
    }

    [Fact]
    public void header_size_with_negative_chain_steps_throws()
    {
        var calculator = SmallCalculator();

        var act = () => calculator.GetHeaderSize(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void production_header_size_zero_chain_steps_equals_base_header_size()
    {
        var calculator = ProductionCalculator();

        calculator.GetHeaderSize(0).Should().Be(Aes256GcmStreamingV2.BaseHeaderSize);
    }

    [Fact]
    public void production_header_size_one_chain_step_adds_step_salt_size()
    {
        var calculator = ProductionCalculator();

        calculator.GetHeaderSize(1).Should().Be(
            Aes256GcmStreamingV2.BaseHeaderSize + KeyDerivationChain.StepSaltSize);
    }

    [Fact]
    public void production_header_size_four_chain_steps()
    {
        var calculator = ProductionCalculator();

        calculator.GetHeaderSize(4).Should().Be(
            Aes256GcmStreamingV2.BaseHeaderSize + 4 * KeyDerivationChain.StepSaltSize);
    }

    #endregion

    #region GetFirstSegmentCiphertextSize

    [Fact]
    public void first_segment_ciphertext_size_shrinks_as_chain_grows()
    {
        var calculator = SmallCalculator();

        calculator.GetFirstSegmentCiphertextSize(0).Should().Be(8);
        calculator.GetFirstSegmentCiphertextSize(1).Should().Be(7);
        calculator.GetFirstSegmentCiphertextSize(2).Should().Be(6);
    }

    [Fact]
    public void production_first_segment_ciphertext_size_with_no_chain()
    {
        var calculator = ProductionCalculator();

        var expected = Aes256GcmStreamingV2.SegmentSize
                     - 16
                     - Aes256GcmStreamingV2.BaseHeaderSize;

        calculator.GetFirstSegmentCiphertextSize(0).Should().Be(expected);
    }

    [Fact]
    public void production_first_segment_ciphertext_size_with_one_chain_step()
    {
        var calculator = ProductionCalculator();

        var expected = Aes256GcmStreamingV2.SegmentSize
                     - 16
                     - Aes256GcmStreamingV2.BaseHeaderSize
                     - KeyDerivationChain.StepSaltSize;

        calculator.GetFirstSegmentCiphertextSize(1).Should().Be(expected);
    }

    #endregion

    #region FindSegment — small calculator, chainStepsCount=0

    [Fact]
    public void find_segment_for_index_in_first_segment_no_chain()
    {
        var calculator = SmallCalculator();

        var segment = calculator.FindSegment(
            encryptedIndex: 5,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        segment.Number.Should().Be(0);
        segment.Start.Should().Be(2);
        segment.End.Should().Be(11);
    }

    [Fact]
    public void find_segment_for_index_at_last_byte_of_first_segment_ciphertext_no_chain()
    {
        var calculator = SmallCalculator();

        var segment = calculator.FindSegment(
            encryptedIndex: 9,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        segment.Number.Should().Be(0);
        segment.Start.Should().Be(2);
        segment.End.Should().Be(11);
    }

    [Fact]
    public void find_segment_for_index_in_second_segment_no_chain()
    {
        var calculator = SmallCalculator();

        var segment = calculator.FindSegment(
            encryptedIndex: 15,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        segment.Number.Should().Be(1);
        segment.Start.Should().Be(12);
        segment.End.Should().Be(23);
    }

    [Fact]
    public void find_segment_for_index_in_third_segment_no_chain()
    {
        var calculator = SmallCalculator();

        var segment = calculator.FindSegment(
            encryptedIndex: 25,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        segment.Number.Should().Be(2);
        segment.Start.Should().Be(24);
        segment.End.Should().Be(35);
    }

    #endregion

    #region FindSegment — small calculator, chainStepsCount=1

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_0_with_one_chain_step()
    {
        var calculator = SmallCalculator();

        // header=3, so first ciphertext byte of segment 0 is at index 3
        var segment = calculator.FindSegment(
            encryptedIndex: 3,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 1);

        segment.Number.Should().Be(0);
        segment.Start.Should().Be(3);
    }

    [Fact]
    public void find_segment_last_ciphertext_byte_of_segment_0_with_one_chain_step()
    {
        var calculator = SmallCalculator();

        // header=3, firstSegmentCiphertextSize=7, so last ciphertext byte is at 3+7-1=9
        var segment = calculator.FindSegment(
            encryptedIndex: 9,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 1);

        segment.Number.Should().Be(0);
    }

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_1_with_one_chain_step()
    {
        var calculator = SmallCalculator();

        // Segment 1 starts after segment 0 (header+ciphertext+tag = 3+7+2 = 12)
        var segment = calculator.FindSegment(
            encryptedIndex: 12,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 1);

        segment.Number.Should().Be(1);
        segment.Start.Should().Be(12);
    }

    [Fact]
    public void find_segment_throws_in_header_region_with_one_chain_step()
    {
        var calculator = SmallCalculator();

        // Header occupies bytes [0..2] when chainStepsCount=1
        var act0 = () => calculator.FindSegment(0, 35, chainStepsCount: 1);
        var act1 = () => calculator.FindSegment(1, 35, chainStepsCount: 1);
        var act2 = () => calculator.FindSegment(2, 35, chainStepsCount: 1);

        act0.Should().Throw<ArgumentOutOfRangeException>();
        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region FromUnencryptedRange — small calculator, chainStepsCount=0

    [Fact]
    public void range_within_first_segment_no_chain()
    {
        var calculator = SmallCalculator();

        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 2, End: 5),
            unencryptedFileSize: 28,
            chainStepsCount: 0);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
        range.FirstSegmentReadStart.Should().Be(2);
        range.LastSegmentReadEnd.Should().Be(5);
    }

    [Fact]
    public void range_crossing_first_and_second_segment_boundary_no_chain()
    {
        var calculator = SmallCalculator();

        // chainStepsCount=0: byte 7 → segment 0 (plaintext [0..7]), byte 10 → segment 1 (plaintext [8..17])
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 6, End: 10),
            unencryptedFileSize: 28,
            chainStepsCount: 0);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
    }

    [Fact]
    public void range_at_exact_boundary_no_chain()
    {
        var calculator = SmallCalculator();

        // Plaintext byte 7 = last of seg 0, byte 8 = first of seg 1 (chainStepsCount=0)
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 7, End: 8),
            unencryptedFileSize: 28,
            chainStepsCount: 0);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
        range.LastSegmentReadEnd.Should().Be(0);
    }

    #endregion

    #region FromUnencryptedRange — chain shifts boundaries

    [Fact]
    public void chain_step_1_shifts_first_segment_boundary()
    {
        var calculator = SmallCalculator();

        // chainStepsCount=1: firstSegmentCiphertextSize=7, so plaintext byte 6 = last of seg 0, byte 7 = first of seg 1
        var rangeAtOldBoundary = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 7, End: 7),
            unencryptedFileSize: 27,
            chainStepsCount: 1);

        rangeAtOldBoundary.FirstSegment.Number.Should().Be(1, "chain shifts the boundary one byte earlier");
        rangeAtOldBoundary.FirstSegmentReadStart.Should().Be(0);
    }

    [Fact]
    public void chain_step_2_shifts_first_segment_boundary_further()
    {
        var calculator = SmallCalculator();

        // chainStepsCount=2: firstSegmentCiphertextSize=6, byte 6 = first of seg 1
        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 6, End: 6),
            unencryptedFileSize: 26,
            chainStepsCount: 2);

        range.FirstSegment.Number.Should().Be(1);
        range.FirstSegmentReadStart.Should().Be(0);
    }

    [Fact]
    public void same_plaintext_byte_lands_in_different_segments_for_different_chain_lengths()
    {
        var calculator = SmallCalculator();

        // Plaintext byte 7:
        //   chainStepsCount=0 → segment 0 (seg 0 holds [0..7])
        //   chainStepsCount=1 → segment 1 (seg 0 holds [0..6])
        //   chainStepsCount=2 → segment 1 (seg 0 holds [0..5])

        var rNoChain = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 7, End: 7),
            unencryptedFileSize: 28,
            chainStepsCount: 0);

        var rOneChain = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 7, End: 7),
            unencryptedFileSize: 27,
            chainStepsCount: 1);

        var rTwoChains = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 7, End: 7),
            unencryptedFileSize: 26,
            chainStepsCount: 2);

        rNoChain.FirstSegment.Number.Should().Be(0);
        rOneChain.FirstSegment.Number.Should().Be(1);
        rTwoChains.FirstSegment.Number.Should().Be(1);
    }

    #endregion

    #region Production constants — boundary tests with chainStepsCount=0

    [Fact]
    public void production_range_within_first_segment_no_chain()
    {
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 0, End: 99),
            unencryptedFileSize: fileSize,
            chainStepsCount: 0);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(0);
        range.FirstSegmentReadStart.Should().Be(0);
        range.LastSegmentReadEnd.Should().Be(99);
    }

    [Fact]
    public void production_range_crossing_first_segment_boundary_no_chain()
    {
        // BaseHeaderSize=42, FirstSegmentCiphertextSize = 1MB - 16 - 42 = 1,048,518
        // Plaintext byte 1,048,517 → seg 0 (last byte); byte 1,048,518 → seg 1 (first byte)

        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_517, End: 1_048_518),
            unencryptedFileSize: fileSize,
            chainStepsCount: 0);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
        range.LastSegmentReadEnd.Should().Be(0);
    }

    [Fact]
    public void production_offsets_are_consistent_with_range_length_no_chain()
    {
        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;
        long rangeStart = 100;
        long rangeEnd = 199;

        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: rangeStart, End: rangeEnd),
            unencryptedFileSize: fileSize,
            chainStepsCount: 0);

        range.FirstSegment.Number.Should().Be(range.LastSegment.Number);
        var readLength = range.LastSegmentReadEnd - range.FirstSegmentReadStart + 1;
        readLength.Should().Be((int)(rangeEnd - rangeStart + 1));
    }

    #endregion

    #region Production constants — boundary tests with non-zero chainStepsCount

    [Fact]
    public void production_first_segment_boundary_with_one_chain_step()
    {
        // chainStepsCount=1 → header = 42 + 32 = 74, firstSegmentCiphertextSize = 1MB - 16 - 74 = 1,048,486
        // Plaintext byte 1,048,485 = last of seg 0; byte 1,048,486 = first of seg 1

        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_485, End: 1_048_486),
            unencryptedFileSize: fileSize,
            chainStepsCount: 1);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
        range.LastSegmentReadEnd.Should().Be(0);
    }

    [Fact]
    public void production_first_segment_boundary_with_two_chain_steps()
    {
        // chainStepsCount=2 → header = 42 + 64 = 106, firstSegmentCiphertextSize = 1,048,454
        // Plaintext byte 1,048,453 = last of seg 0; byte 1,048,454 = first of seg 1

        var calculator = ProductionCalculator();
        long fileSize = 2 * 1024 * 1024;

        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_453, End: 1_048_454),
            unencryptedFileSize: fileSize,
            chainStepsCount: 2);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);
        range.LastSegmentReadEnd.Should().Be(0);
    }

    [Fact]
    public void production_byte_at_old_boundary_lands_in_segment_1_when_chain_grows()
    {
        // Plaintext byte 1,048,515 — last byte of segment 0 when chainStepsCount=0,
        // but with chainStepsCount=1 segment 0 only holds bytes [0..1,048,483],
        // so 1,048,515 is well inside segment 1.

        var calculator = ProductionCalculator();
        long fileSize = 4 * 1024 * 1024;

        var rNoChain = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_515, End: 1_048_515),
            unencryptedFileSize: fileSize,
            chainStepsCount: 0);

        var rOneChain = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: 1_048_515, End: 1_048_515),
            unencryptedFileSize: fileSize,
            chainStepsCount: 1);

        rNoChain.FirstSegment.Number.Should().Be(0);
        rOneChain.FirstSegment.Number.Should().Be(1);
    }

    [Fact]
    public void production_cross_boundary_offsets_total_with_one_chain_step()
    {
        // chainStepsCount=1: firstSegmentCiphertextSize = 1,048,486
        // Range [1,048,400 .. 1,048,500] crosses seg 0 → seg 1.

        var calculator = ProductionCalculator();
        var firstSegmentCiphertextSize = 1_048_486;
        long fileSize = 2 * 1024 * 1024;
        long rangeStart = 1_048_400;
        long rangeEnd = 1_048_500;
        var expectedLength = rangeEnd - rangeStart + 1;

        var range = calculator.FromUnencryptedRange(
            unencryptedRange: new BytesRange(Start: rangeStart, End: rangeEnd),
            unencryptedFileSize: fileSize,
            chainStepsCount: 1);

        range.FirstSegment.Number.Should().Be(0);
        range.LastSegment.Number.Should().Be(1);

        var fromFirstSegment = firstSegmentCiphertextSize - range.FirstSegmentReadStart;
        var fromLastSegment = range.LastSegmentReadEnd + 1;
        var totalBytes = fromFirstSegment + fromLastSegment;

        totalBytes.Should().Be((int)expectedLength,
            $"FirstSegmentReadOffset={range.FirstSegmentReadStart}, LastSegmentReadOffset={range.LastSegmentReadEnd}");
    }

    #endregion

    #region FindSegment — guards and exact boundary validation, chainStepsCount=0

    [Fact]
    public void find_segment_throws_for_negative_index()
    {
        var calculator = SmallCalculator();

        var act = () => calculator.FindSegment(
            encryptedIndex: -1,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_header_no_chain()
    {
        var calculator = SmallCalculator();

        var act0 = () => calculator.FindSegment(0, 35, chainStepsCount: 0);
        var act1 = () => calculator.FindSegment(1, 35, chainStepsCount: 0);

        act0.Should().Throw<ArgumentOutOfRangeException>();
        act1.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_beyond_file()
    {
        var calculator = SmallCalculator();

        var act = () => calculator.FindSegment(
            encryptedIndex: 36,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_segment_0_tag_no_chain()
    {
        var calculator = SmallCalculator();

        // chainStepsCount=0: segment 0 tag is at bytes [10..11]
        var act10 = () => calculator.FindSegment(10, 35, chainStepsCount: 0);
        var act11 = () => calculator.FindSegment(11, 35, chainStepsCount: 0);

        act10.Should().Throw<ArgumentOutOfRangeException>();
        act11.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_segment_1_tag_no_chain()
    {
        var calculator = SmallCalculator();

        // Segment 1 tag is at [22..23] regardless of chain length (only first segment header changes)
        var act22 = () => calculator.FindSegment(22, 35, chainStepsCount: 0);
        var act23 = () => calculator.FindSegment(23, 35, chainStepsCount: 0);

        act22.Should().Throw<ArgumentOutOfRangeException>();
        act23.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_throws_for_index_in_segment_2_tag_no_chain()
    {
        var calculator = SmallCalculator();

        var act34 = () => calculator.FindSegment(34, 35, chainStepsCount: 0);
        var act35 = () => calculator.FindSegment(35, 35, chainStepsCount: 0);

        act34.Should().Throw<ArgumentOutOfRangeException>();
        act35.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_0_no_chain()
    {
        var calculator = SmallCalculator();

        var segment = calculator.FindSegment(
            encryptedIndex: 2,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        segment.Number.Should().Be(0);
    }

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_1_no_chain()
    {
        var calculator = SmallCalculator();

        var segment = calculator.FindSegment(
            encryptedIndex: 12,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        segment.Number.Should().Be(1);
        segment.Start.Should().Be(12);
    }

    [Fact]
    public void find_segment_first_ciphertext_byte_of_segment_2_no_chain()
    {
        var calculator = SmallCalculator();

        var segment = calculator.FindSegment(
            encryptedIndex: 24,
            encryptedFileLastByteIndex: 35,
            chainStepsCount: 0);

        segment.Number.Should().Be(2);
        segment.Start.Should().Be(24);
    }

    #endregion
}
