using System.Buffers;
using System.IO.Pipelines;

namespace PlikShare.Core.Utils;

public static class PipeReaderExtensions
{
    public static async ValueTask CopyTo(
        this PipeReader reader,
        Memory<byte> output,
        int sizeInBytes,
        CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;

        while (totalBytesRead < sizeInBytes)
        {
            var result = await reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            if (result.IsCanceled)
            {
                throw new OperationCanceledException();
            }

            var bytesToCopy = (int) Math.Min(
                buffer.Length, 
                sizeInBytes - totalBytesRead);

            if (bytesToCopy > 0)
            {
                buffer.Slice(0, bytesToCopy).CopyTo(
                    output.Span.Slice(totalBytesRead, bytesToCopy));

                totalBytesRead += bytesToCopy;

                reader.AdvanceTo(
                    buffer.GetPosition(bytesToCopy));
            }

            if (result.IsCompleted && totalBytesRead < sizeInBytes)
            {
                throw new EndOfStreamException($"Reached end of stream after reading {totalBytesRead} bytes. Expected {sizeInBytes} bytes.");
            }
        }
    }
}