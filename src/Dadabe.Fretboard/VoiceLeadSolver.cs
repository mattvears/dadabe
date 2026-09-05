using System.Collections.Immutable;

namespace Dadabe.Fretboard;

/// <summary>
/// Represents one solution to a voice-leading problem: a sequence of voicings
/// (one per chord in the progression) chosen to minimise total fret travel.
/// </summary>
public sealed record VoiceLeadSolution(
    int TotalDistance,
    IReadOnlyList<VoiceLeadStep> Steps);

/// <summary>
/// One step in a voice-leading solution: the chosen voicing for a chord and
/// the fret-distance cost of arriving here from the previous voicing
/// (null for the first step).
/// </summary>
public sealed record VoiceLeadStep(Voicing Voicing, int? TransitionDistance);

/// <summary>
/// Finds the k cheapest voice-leading paths through a chord progression (D29).
/// Uses backward DP to find the single best path, then A* with exact cost-to-go
/// heuristic to enumerate additional solutions in order.
/// </summary>
public static class VoiceLeadSolver
{
    /// <summary>
    /// Solve the progression.
    /// </summary>
    /// <param name="voicingsPerChord">
    /// One list of candidate voicings per chord, in progression order.
    /// All voicings must share the same tuning.
    /// </param>
    /// <param name="maxSolutions">
    /// Maximum number of distinct solutions to return (1–10).
    /// </param>
    public static IReadOnlyList<VoiceLeadSolution> Solve(
        IReadOnlyList<ImmutableArray<Voicing>> voicingsPerChord,
        int maxSolutions)
    {
        ArgumentNullException.ThrowIfNull(voicingsPerChord);
        int n = voicingsPerChord.Count;
        if (n == 0) return [];
        if (maxSolutions <= 0) return [];

        // Single-chord progression: every voicing is its own zero-cost solution.
        if (n == 1)
        {
            var singles = voicingsPerChord[0]
                .Take(maxSolutions)
                .Select(v => new VoiceLeadSolution(0, [new VoiceLeadStep(v, null)]))
                .ToList();
            return singles;
        }

        // Precompute pairwise distances: dist[layer][fromIdx][toIdx]
        var dist = new int[n - 1][][];
        for (int layer = 0; layer < n - 1; layer++)
        {
            var from = voicingsPerChord[layer];
            var to = voicingsPerChord[layer + 1];
            dist[layer] = new int[from.Length][];
            for (int j = 0; j < from.Length; j++)
            {
                dist[layer][j] = new int[to.Length];
                for (int k = 0; k < to.Length; k++)
                {
                    dist[layer][j][k] = TotalFretDistance(from[j], to[k]);
                }
            }
        }

        // Backward DP: minCostToEnd[layer][voicingIdx] = cheapest cost from this voicing to end.
        var minCostToEnd = new int[n][];
        for (int layer = 0; layer < n; layer++)
        {
            minCostToEnd[layer] = new int[voicingsPerChord[layer].Length];
        }
        // Last layer: cost to end = 0 for every voicing.
        // Fill backwards.
        for (int layer = n - 2; layer >= 0; layer--)
        {
            var fromCount = voicingsPerChord[layer].Length;
            var toCount = voicingsPerChord[layer + 1].Length;
            for (int j = 0; j < fromCount; j++)
            {
                int best = int.MaxValue;
                for (int k = 0; k < toCount; k++)
                {
                    int cost = dist[layer][j][k] + minCostToEnd[layer + 1][k];
                    if (cost < best) best = cost;
                }
                minCostToEnd[layer][j] = best;
            }
        }

        // A* priority queue: (f = g + h, g = cost so far, layer, voicingIdx, parentRef)
        // parentRef encodes the chain as a singly-linked list node.
        var heap = new PriorityQueue<PathNode, int>();
        for (int j = 0; j < voicingsPerChord[0].Length; j++)
        {
            int h = minCostToEnd[0][j];
            heap.Enqueue(new PathNode(g: 0, Layer: 0, VoicingIdx: j, Parent: null), priority: h);
        }

        var results = new List<VoiceLeadSolution>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (heap.Count > 0 && results.Count < maxSolutions)
        {
            var node = heap.Dequeue();
            if (node.Layer == n - 1)
            {
                // Complete path — reconstruct and deduplicate.
                var path = ReconstructPath(node, n);
                var key = BuildKey(path, voicingsPerChord);
                if (seen.Add(key))
                {
                    results.Add(BuildSolution(path, voicingsPerChord, dist));
                }
                continue;
            }

            // Extend one layer forward. su
            int nextLayer = node.Layer + 1;
            var nextVoicings = voicingsPerChord[nextLayer];
            for (int k = 0; k < nextVoicings.Length; k++)
            {
                int edgeCost = dist[node.Layer][node.VoicingIdx][k];
                int newG = node.g + edgeCost;
                int newH = minCostToEnd[nextLayer][k];
                var child = new PathNode(g: newG, Layer: nextLayer, VoicingIdx: k, Parent: node);
                heap.Enqueue(child, priority: newG + newH);
            }
        }

        return results;
    }

    /// <summary>
    /// Computes the total fret-distance cost of transitioning from voicing
    /// <paramref name="a"/> to voicing <paramref name="b"/> (D29).
    /// Muted strings use -1 as a sentinel so muted↔fretted counts as a move.
    /// </summary>
    public static int TotalFretDistance(Voicing a, Voicing b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        int stringCount = a.Tuning.Strings.Length;
        // Build fret lookup by string index (muted = -1).
        Span<int> fretsA = stackalloc int[stringCount];
        Span<int> fretsB = stackalloc int[stringCount];
        for (int i = 0; i < stringCount; i++) { fretsA[i] = -1; fretsB[i] = -1; }

        foreach (var p in a.Positions)
        {
            if (p.String < stringCount)
                fretsA[p.String] = p.Fret ?? -1;
        }
        foreach (var p in b.Positions)
        {
            if (p.String < stringCount)
                fretsB[p.String] = p.Fret ?? -1;
        }

        int total = 0;
        for (int s = 0; s < stringCount; s++)
            total += Math.Abs(fretsA[s] - fretsB[s]);
        return total;
    }

    /// <summary>
    /// Builds a <see cref="Transition"/> domain object from two consecutive voicings.
    /// </summary>
    public static Transition BuildTransition(Voicing from, Voicing to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        int stringCount = from.Tuning.Strings.Length;
        var moves = new List<VoiceMove>(stringCount);
        Span<int> fretsFrom = stackalloc int[stringCount];
        Span<int> fretsTo = stackalloc int[stringCount];
        for (int i = 0; i < stringCount; i++) { fretsFrom[i] = -1; fretsTo[i] = -1; }

        foreach (var p in from.Positions)
            if (p.String < stringCount) fretsFrom[p.String] = p.Fret ?? -1;
        foreach (var p in to.Positions)
            if (p.String < stringCount) fretsTo[p.String] = p.Fret ?? -1;

        int total = 0;
        for (int s = 0; s < stringCount; s++)
        {
            int d = Math.Abs(fretsFrom[s] - fretsTo[s]);
            total += d;
            if (d > 0)
            {
                moves.Add(new VoiceMove(
                    StringIndex: s,
                    FromFret: fretsFrom[s] == -1 ? null : fretsFrom[s],
                    ToFret: fretsTo[s] == -1 ? null : fretsTo[s],
                    Distance: d));
            }
        }
        return new Transition(from, to, moves, total);
    }

    // -- helpers --

    private sealed record PathNode(int g, int Layer, int VoicingIdx, PathNode? Parent);

    private static int[] ReconstructPath(PathNode terminal, int n)
    {
        var path = new int[n];
        var cur = terminal;
        while (cur is not null)
        {
            path[cur.Layer] = cur.VoicingIdx;
            cur = cur.Parent;
        }
        return path;
    }

    private static string BuildKey(int[] path, IReadOnlyList<ImmutableArray<Voicing>> voicingsPerChord)
    {
        var parts = new string[path.Length];
        for (int i = 0; i < path.Length; i++)
            parts[i] = voicingsPerChord[i][path[i]].ContentHash.ToString();
        return string.Join("|", parts);
    }

    private static VoiceLeadSolution BuildSolution(
        int[] path,
        IReadOnlyList<ImmutableArray<Voicing>> voicingsPerChord,
        int[][][] dist)
    {
        var steps = new List<VoiceLeadStep>(path.Length);
        int totalDist = 0;
        for (int layer = 0; layer < path.Length; layer++)
        {
            int? transitionDist = null;
            if (layer > 0)
            {
                int d = dist[layer - 1][path[layer - 1]][path[layer]];
                totalDist += d;
                transitionDist = d;
            }
            steps.Add(new VoiceLeadStep(voicingsPerChord[layer][path[layer]], transitionDist));
        }
        return new VoiceLeadSolution(totalDist, steps);
    }
}
