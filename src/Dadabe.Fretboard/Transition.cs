using Dadabe.Core.Memo;

namespace Dadabe.Fretboard;

/// <summary>
/// The per-string fret move from one voicing to the next (D29).
/// FromFret/ToFret are null for muted strings; Distance uses -1 as the
/// muted sentinel so muted↔fretted counts as a full-span move.
/// </summary>
public sealed record VoiceMove(
    int StringIndex,
    int? FromFret,
    int? ToFret,
    int Distance);

/// <summary>
/// A transition between two consecutive voicings: the full per-string
/// move set and the aggregate fret-distance cost (D29). Content-hashable
/// so the memo layer can cache repeated chord-pair costs.
/// </summary>
public sealed record Transition(
    Voicing From,
    Voicing To,
    IReadOnlyList<VoiceMove> Moves,
    int TotalDistance) : IContentHashable
{
    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical()
                .HashDigest(From.ContentHash)
                .HashDigest(To.ContentHash);
            return ContentHash.FromCanonical(Namespaces.Transition, 1, c.AsSpan());
        }
    }
}
