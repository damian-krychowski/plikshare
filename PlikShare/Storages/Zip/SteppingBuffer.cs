namespace PlikShare.Storages.Zip;

public sealed class SteppingBuffer(byte size)
{
    private readonly Memory<byte> _memory = new byte[size];
    private readonly int _size = size;
    private int _position;
    private bool _wrapped;

    public void Push(byte value)
    {
        _memory.Span[_position] = value;
        _position = (_position + 1) % _size;
        if (_position == 0)
            _wrapped = true;
    }

    public ReadOnlySpan<byte> GetSpan()
    {
        if (!_wrapped)
            return _memory.Span[.._position];

        if (_position == 0)
            return _memory.Span;

        Rotate(_memory.Span, _position);
        return _memory.Span;
    }

    private static void Rotate(Span<byte> arr, int rotate)
    {
        var size = arr.Length;

        int i;
        var gcd = Gcd(rotate, size);

        for (i = 0; i < gcd; i++)
        {
            var temp = arr[i];
            var j = i;

            while (true)
            {
                var k = j + rotate;
                if (k >= size)
                    k = k - size;

                if (k == i)
                    break;

                arr[j] = arr[k];
                j = k;
            }

            arr[j] = temp;
        }
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}