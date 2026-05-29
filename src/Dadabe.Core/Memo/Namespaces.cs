namespace Dadabe.Core.Memo;

/// <summary>
/// Stable namespace strings for every memo cache in the project (D18 §8.3).
/// The v0.1-wired set is at the top; v0.2-reserved ones are declared but
/// unused until those features arrive.
/// </summary>
public static class Namespaces
{
    // Identity-bearing domain types.
    public const string Tuning = "tuning";
    public const string ChordSpec = "chord-spec";
    public const string HandModel = "hand-model";
    public const string Voicing = "voicing";
    public const string Fingering = "fingering";
    public const string Transition = "transition";

    // Memo caches (wired v0.1).
    public const string VoicingSearch = "voicing-search";
    public const string Classification = "classification";

    // Memo caches (reserved v0.2).
    public const string Progression = "progression";
    public const string Transform = "transform";
    public const string InnerLine = "inner-line";
    public const string MelodyVoicings = "melody-voicings";

    // Search-key wrappers content-hashed for memo lookup.
    public const string ChordSymbol = "chord-symbol";
    public const string VoicingSearchKey = "voicing-search-key";
    public const string FingeringKey = "fingering-key";
}
