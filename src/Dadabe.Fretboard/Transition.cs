using System.Collections.Immutable;
using Dadabe.Core;
using Dadabe.Core.Memo;

namespace Dadabe.Fretboard;

/// <summary>
/// One voice moving from <see cref="From"/> to <see cref="To"/>. Type-only
/// in v0.1 (D16); no command produces one. v0.2 progression features will
/// be additive.
/// </summary>
public sealed record VoiceMove(Pitch From, Pitch To, int Semitones);

/// <summary>
/// A transition between two voicings — the full set of per-voice moves
/// plus the aggregate motion. Type-only in v0.1 (D16); content-hashable
/// so v0.2 caches can key on it (namespace <c>transition</c>).
/// </summary>
public sealed record Transition(
    Voicing From,
    Voicing To,
    ImmutableArray<VoiceMove> Moves,
    int TotalSemitones) : IContentHashable
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
