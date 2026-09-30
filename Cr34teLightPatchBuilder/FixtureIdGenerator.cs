using System.Globalization;

namespace Cr34teLightPatchBuilder;

public static class FixtureIdGenerator
{
    public static int FillMissing(IReadOnlyList<PatchEntry> entries, IProgress<int>? progress = null)
    {
        var lastProgress = -1;
        void ReportProgress(int value)
        {
            if (progress is null || value <= lastProgress)
            {
                return;
            }

            lastProgress = value;
            progress.Report(value);
        }

        ReportProgress(0);
        if (entries.Count == 0)
        {
            ReportProgress(100);
            return 0;
        }

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedNumericIds = new HashSet<int>();
        var missingCount = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            var fixtureId = entries[index].FixtureId.Trim();
            if (string.IsNullOrWhiteSpace(fixtureId))
            {
                missingCount++;
            }
            else
            {
                usedIds.Add(fixtureId);
                if (int.TryParse(fixtureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
                {
                    usedNumericIds.Add(value);
                }
            }

            ReportProgress((int)((index + 1) * 50L / entries.Count));
        }

        if (missingCount == 0)
        {
            ReportProgress(100);
            return 0;
        }

        var nextId = 1;
        var filled = 0;

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!string.IsNullOrWhiteSpace(entry.FixtureId))
            {
                ReportProgress(50 + (int)((index + 1) * 50L / entries.Count));
                continue;
            }

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
            ReportProgress(50 + (int)((index + 1) * 50L / entries.Count));
        }

        ReportProgress(100);
        return filled;
    }
}
