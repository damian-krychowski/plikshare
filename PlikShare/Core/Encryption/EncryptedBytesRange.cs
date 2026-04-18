using PlikShare.Files.PreSignedLinks.RangeRequests;

/// <summary>
/// Describes an encrypted byte range spanning one or more ciphertext segments in an
/// AES-GCM encrypted file, together with the precise inclusive cut points that trim
/// the decrypted output down to the caller's requested plaintext range.
/// </summary>
/// <remarks>
/// <para>
/// An encrypted file is organized as a sequence of segments. Each segment is an
/// independently authenticated AES-GCM unit consisting of ciphertext followed by a
/// 16-byte authentication tag. Segment 0 additionally carries a fixed-size header
/// at its start (holding key version, salt and nonce prefix + more depending on the version),
/// which leaves it with slightly less ciphertext capacity than subsequent segments;
/// on disk every segment occupies the same <see cref="Aes256GcmStreamingV1.SegmentSize"/> bytes.
/// </para>
/// <para>
/// AES-GCM does not permit partial decryption of a segment — the full ciphertext
/// plus its tag are needed to authenticate and recover any plaintext from it. To
/// serve a plaintext range request, whole segments covering that range must
/// therefore be read and decrypted, even when the caller's range starts partway
/// through the first of those segments or ends partway through the last.
/// </para>
/// <para>
/// <see cref="FirstSegmentReadStart"/> and <see cref="LastSegmentReadEnd"/> express
/// those trim points as <b>inclusive</b> indices into the decrypted plaintext of
/// the first and last selected segment respectively, matching the inclusive-inclusive
/// convention used by <see cref="BytesRange"/>. The number of plaintext bytes
/// emitted from the last segment is therefore
/// <c>LastSegmentReadEnd - startIndex + 1</c>, where <c>startIndex</c> is
/// <see cref="FirstSegmentReadStart"/> when a single segment spans the whole range
/// and <c>0</c> otherwise.
/// </para>
/// <para>
/// When the range is fully contained in a single segment, <see cref="FirstSegment"/>
/// and <see cref="LastSegment"/> refer to the same segment, and both trim points
/// apply to it.
/// </para>
/// </remarks>
/// <param name="FirstSegment">
/// The first ciphertext segment (including its tag, and — for segment 0 — its
/// header) that must be read from storage in order to serve the requested plaintext
/// range.
/// </param>
/// <param name="LastSegment">
/// The last ciphertext segment (including its tag) that must be read from storage
/// in order to serve the requested plaintext range. Equal to
/// <paramref name="FirstSegment"/> when the requested range falls within a single
/// segment.
/// </param>
/// <param name="FirstSegmentReadStart">
/// Inclusive index, within the decrypted plaintext of <paramref name="FirstSegment"/>,
/// of the first byte to emit to the caller. Plaintext bytes before this index are
/// produced by the decryption of the segment (which is atomic in AES-GCM) but
/// discarded before the output is written.
/// </param>
/// <param name="LastSegmentReadEnd">
/// Inclusive index, within the decrypted plaintext of <paramref name="LastSegment"/>,
/// of the last byte to emit to the caller. Plaintext bytes after this index are
/// produced by the decryption of the segment (which is atomic in AES-GCM) but
/// discarded before the output is written.
/// </param>
public record EncryptedBytesRange(
    EncryptedBytesRange.Segment FirstSegment,
    EncryptedBytesRange.Segment LastSegment,
    int FirstSegmentReadStart,
    int LastSegmentReadEnd)
{
    /// <summary>
    /// Identifies a single ciphertext segment within an encrypted file by its
    /// ordinal number and its absolute byte bounds in the encrypted stream.
    /// </summary>
    /// <param name="Number">
    /// Zero-based ordinal of the segment in the file. Segment 0 is the segment that
    /// begins at the start of the encrypted file and carries the file header
    /// immediately before its ciphertext; subsequent segments hold only ciphertext
    /// and a tag.
    /// </param>
    /// <param name="Start">
    /// Inclusive byte offset within the encrypted file at which this segment's
    /// ciphertext begins. For segment 0 this is the byte immediately after the
    /// header; for subsequent segments it is the byte immediately after the
    /// previous segment's authentication tag. Equivalently, this is the position
    /// from which a reader must start consuming bytes to decrypt this segment
    /// (the header has already been accounted for in segment 0's offset).
    /// </param>
    /// <param name="End">
    /// Inclusive byte offset within the encrypted file of this segment's last
    /// byte — the final byte of its authentication tag.
    /// </param>
    public readonly record struct Segment(
        int Number,
        long Start,
        long End);

    /// <summary>
    /// Projects this encrypted range into the absolute byte span that must be
    /// fetched from storage: from the first byte of <see cref="FirstSegment"/>'s
    /// ciphertext to the last byte of <see cref="LastSegment"/>'s authentication
    /// tag, inclusive.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="BytesRange"/> refers to positions in the
    /// <b>encrypted</b> file, not the plaintext. It always covers whole segments,
    /// regardless of where <see cref="FirstSegmentReadStart"/> and
    /// <see cref="LastSegmentReadEnd"/> fall within them. Note that when
    /// <see cref="FirstSegment"/> is segment 0, the header sits immediately before
    /// <c>FirstSegment.Start</c> and is <i>not</i> included in the returned range —
    /// this matches the range consumers that assume the reader is already
    /// positioned at the start of a segment's ciphertext.
    /// </remarks>
    public BytesRange ToBytesRange()
    {
        return new BytesRange(
            Start: FirstSegment.Start,
            End: LastSegment.End);
    }
}