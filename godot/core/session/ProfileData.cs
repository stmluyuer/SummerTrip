using System;
using System.Collections.Generic;
using System.Linq;

namespace SummerTrip.Core.Session;

public sealed record ProfileSnapshot(
    int ProfileVersion,
    IReadOnlyList<string> ReadLineIds,
    IReadOnlyList<string> UnlockedEndings,
    IReadOnlyList<string> UnlockedCgs,
    IReadOnlyList<string> Achievements
);

public sealed class ProfileData
{
    public const int CurrentVersion = 1;

    public HashSet<string> ReadLineIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UnlockedEndings { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UnlockedCgs { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Achievements { get; } = new(StringComparer.Ordinal);

    public ProfileSnapshot CaptureSnapshot()
    {
        return new ProfileSnapshot(
            CurrentVersion,
            Sorted(ReadLineIds),
            Sorted(UnlockedEndings),
            Sorted(UnlockedCgs),
            Sorted(Achievements)
        );
    }

    public ProfileData Clone()
    {
        var clone = new ProfileData();
        clone.RestoreSnapshot(CaptureSnapshot());
        return clone;
    }

    public void MergeReadLineIds(IEnumerable<string> staticLineIds)
    {
        ArgumentNullException.ThrowIfNull(staticLineIds);
        foreach (var staticLineId in staticLineIds)
        {
            if (string.IsNullOrWhiteSpace(staticLineId))
            {
                throw new ArgumentException("Profile read line IDs cannot be empty.");
            }

            ReadLineIds.Add(staticLineId);
        }
    }

    public void RestoreSnapshot(ProfileSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.ProfileVersion != CurrentVersion)
        {
            throw new ArgumentException(
                $"Unsupported profile version {snapshot.ProfileVersion}.",
                nameof(snapshot)
            );
        }

        Replace(ReadLineIds, snapshot.ReadLineIds);
        Replace(UnlockedEndings, snapshot.UnlockedEndings);
        Replace(UnlockedCgs, snapshot.UnlockedCgs);
        Replace(Achievements, snapshot.Achievements);
    }

    public void ReplaceWith(ProfileData other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RestoreSnapshot(other.CaptureSnapshot());
    }

    private static string[] Sorted(IEnumerable<string> values)
    {
        return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void Replace(HashSet<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Profile IDs cannot be empty.");
            }

            target.Add(value);
        }
    }
}
