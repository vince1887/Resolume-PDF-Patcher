using System.Globalization;

namespace Cr34teLightPatchBuilder;

public static class FixtureIdGenerator
{
    public static int FillMissing(IReadOnlyList<PatchEntry> entries)
    {
        var usedIds = entries
            .Select(entry => entry.FixtureId.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedNumericIds = usedIds
            .Select(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToHashSet();
        var nextId = 1;
        var filled = 0;

        foreach (var entry in entries.Where(entry => string.IsNullOrWhiteSpace(entry.FixtureId)))
        {
            while (usedNumericIds.Contains(nextId) ||
                   usedIds.Contains(nextId.ToString(CultureInfo.InvariantCulture)))
            {
                if (nextId == int.MaxValue)
                {
                    throw new InvalidOperationException("No unused numeric fixture IDs remain.");
                }

                nextId++;
            }

            var id = nextId.ToString(CultureInfo.InvariantCulture);
            entry.FixtureId = id;
            usedIds.Add(id);
            usedNumericIds.Add(nextId);
            if (nextId < int.MaxValue)
            {
                nextId++;
            }

            filled++;
        }

        return filled;
    }
}
