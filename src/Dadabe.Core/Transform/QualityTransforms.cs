using System.Collections.Immutable;
using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>Chord-shape guards shared by the quality-family transforms (D53).</summary>
internal static class ChordShapeGuards
{
    /// <summary>True if the form has no third (power chord, sus2, sus4).</summary>
    public static bool IsThirdless(GrammarForm form) =>
        !form.Required.Contains("3", StringComparer.Ordinal) && !form.Required.Contains("b3", StringComparer.Ordinal);

    /// <summary>True if the form is a plain major or minor triad with no extensions/alterations.</summary>
    public static bool IsBareTriad(ChordSymbol chord) =>
        chord.Extensions.IsEmpty && chord.Alterations.IsEmpty
        && chord.Quality is "maj" or "" or "m";

    /// <summary>True if the form requires both 3 and b7 (dominant quality).</summary>
    public static bool IsDominant(GrammarForm form) =>
        form.Required.Contains("3", StringComparer.Ordinal) && form.Required.Contains("b7", StringComparer.Ordinal);
}

/// <summary>
/// <c>quality-map</c> (D47): blanket quality replacement, roots unchanged.
/// Skips thirdless chords (D53). Not invertible — the original quality is lost.
/// </summary>
public sealed class QualityMapTransform : ITransformation
{
    private readonly ChordGrammar _grammar;
    private readonly Dictionary<string, GrammarForm> _formsByDisplayName;

    public QualityMapTransform(ChordGrammar grammar)
    {
        _grammar = grammar;
        _formsByDisplayName = grammar.Forms.ToDictionary(f => f.DisplayName, StringComparer.Ordinal);
    }

    public string Id => "quality-map";
    public string DisplayName => "Map Quality";
    public bool IsInvertible => false;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var to = TransformParams.GetString(parameters, "to")
            ?? throw new ArgumentException("quality-map requires a 'to' quality.");
        if (!_formsByDisplayName.ContainsKey(to))
        {
            throw new ArgumentException($"'{to}' is not a quality in the grammar.");
        }

        var notes = new List<TransformNote>();
        var results = new List<ChordSymbol>(chords.Count);

        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            var form = _formsByDisplayName[chord.Quality];
            if (ChordShapeGuards.IsThirdless(form))
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' has no third — quality-map is a no-op on thirdless chords."));
                results.Add(chord);
                continue;
            }
            results.Add(chord with { Quality = to, Extensions = ImmutableArray<string>.Empty, Alterations = ImmutableArray<string>.Empty });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: false);
    }
}

/// <summary>
/// <c>reduce</c> (D52): mechanical simplification toward the triad or
/// seventh core. Not invertible — extensions are discarded.
/// </summary>
public sealed class ReduceTransform : ITransformation
{
    public string Id => "reduce";
    public string DisplayName => "Reduce";
    public bool IsInvertible => false;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var level = TransformParams.GetString(parameters, "level") ?? "triad";
        if (level is not ("triad" or "seventh"))
        {
            throw new ArgumentException("reduce requires level 'triad' or 'seventh'.");
        }

        var notes = new List<TransformNote>();
        var results = new List<ChordSymbol>(chords.Count);
        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            if (!ChordApproximation.CoreByQuality.TryGetValue(chord.Quality, out var core))
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' has no known reduction core."));
                results.Add(chord);
                continue;
            }
            var target = level == "triad" ? core.Triad : core.Seventh;
            results.Add(chord with { Quality = target, Extensions = ImmutableArray<string>.Empty, Alterations = ImmutableArray<string>.Empty });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: false);
    }
}

/// <summary>
/// <c>tritone-sub</c> (D48): dominant chords only — root moves +A4, quality
/// kept. Slash-chord bass is dropped and reported, since it was chosen for
/// the chord being replaced. Invertible (apply again to return, since +A4
/// twice is a full tritone = octave).
/// </summary>
public sealed class TritoneSubTransform : ITransformation
{
    private readonly Dictionary<string, GrammarForm> _formsByDisplayName;

    public TritoneSubTransform(ChordGrammar grammar)
    {
        _formsByDisplayName = grammar.Forms.ToDictionary(f => f.DisplayName, StringComparer.Ordinal);
    }

    public string Id => "tritone-sub";
    public string DisplayName => "Tritone Substitution";
    public bool IsInvertible => true;

    private static readonly Interval AugmentedFourth = new(6, "A4");

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var positions = TransformParams.GetPositions(parameters, chords.Count);
        var notes = new List<TransformNote>();
        var results = new List<ChordSymbol>(chords.Count);

        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            if (!positions.Contains(i))
            {
                results.Add(chord);
                continue;
            }

            var form = _formsByDisplayName[chord.Quality];
            if (!ChordShapeGuards.IsDominant(form))
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' is not a dominant chord — tritone-sub requires 3 and b7."));
                results.Add(chord);
                continue;
            }

            var newRoot = chord.Root.Transpose(AugmentedFourth, out var spellingNote);
            if (spellingNote is not null) { notes.Add(new TransformNote(i, "respelled", spellingNote)); }
            if (chord.Bass is not null)
            {
                notes.Add(new TransformNote(i, "bass-dropped", $"Bass on '{chord.ToSymbol()}' was chosen for the original chord and is dropped under tritone-sub."));
            }
            results.Add(chord with { Root = newRoot, Bass = null });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: true);
    }
}

/// <summary>
/// <c>plr</c> (D48): Neo-Riemannian P/L/R on bare major/minor triads only.
/// Non-triads are skipped and reported (D53). Each operation is its own inverse.
/// </summary>
public sealed class PlrTransform : ITransformation
{
    public string Id => "plr";
    public string DisplayName => "Neo-Riemannian PLR";
    public bool IsInvertible => true;

    // Root motion applied when moving major -> minor; the reverse (minor -> major)
    // uses the complementary motion so each op is its own inverse (D48).
    private static readonly Dictionary<string, (Interval MajorToMinor, Interval MinorToMajor)> Motions = new(StringComparer.Ordinal)
    {
        ["L"] = (new Interval(4, "M3"), new Interval(8, "m6")),
        ["R"] = (new Interval(9, "M6"), new Interval(3, "m3")),
    };

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var op = TransformParams.GetString(parameters, "op")
            ?? throw new ArgumentException("plr requires an 'op' of P, L, or R.");
        if (op is not ("P" or "L" or "R"))
        {
            throw new ArgumentException("plr 'op' must be P, L, or R.");
        }

        var notes = new List<TransformNote>();
        var results = new List<ChordSymbol>(chords.Count);

        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            if (!ChordShapeGuards.IsBareTriad(chord))
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' is not a bare triad — plr requires a plain major or minor triad."));
                results.Add(chord);
                continue;
            }

            var isMajor = chord.Quality is "maj" or "";
            if (op == "P")
            {
                results.Add(chord with { Quality = isMajor ? "m" : "" });
                continue;
            }

            var (majorToMinor, minorToMajor) = Motions[op];
            var interval = isMajor ? majorToMinor : minorToMajor;
            var newRoot = chord.Root.Transpose(interval, out var spellingNote);
            if (spellingNote is not null) { notes.Add(new TransformNote(i, "respelled", spellingNote)); }
            results.Add(chord with { Root = newRoot, Quality = isMajor ? "m" : "" });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: true);
    }
}
