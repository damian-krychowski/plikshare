namespace PlikShare.Core.Utils;

public readonly record struct IdsRange(int Start, int End)
{
    public override string ToString()
    {
        return Start == End
            ? $"{Start}"
            : (Start + 1) == End 
                ? $"{Start}, {End}"
                : $"({Start}:{End})";
    }

    public int Count() => End - Start + 1;

    public static IdsRangeCollection GroupConsecutiveIds(IEnumerable<int> ids)
    {
        var sortedNumbers = ids
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (sortedNumbers.Count == 0)
            return new IdsRangeCollection([]);

        var result = new List<IdsRange>();
        var rangeStart = sortedNumbers[0];
        var prev = rangeStart;

        for (var i = 1; i < sortedNumbers.Count; i++)
        {
            if (sortedNumbers[i] != prev + 1)
            {
                result.Add(new IdsRange(rangeStart, prev));
                rangeStart = sortedNumbers[i];
            }
            prev = sortedNumbers[i];
        }

        result.Add(new IdsRange(rangeStart, prev));

        return new IdsRangeCollection(result);
    }
}

public class IdsRangeCollection(List<IdsRange> ranges)
{
    public List<IdsRange> Ranges { get; } = ranges;
    
    public override string ToString()
    {
        return string.Join(", ", Ranges);
    }
}