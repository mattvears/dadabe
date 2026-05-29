using Xunit.Abstractions;

namespace Dadabe.Cli.Tests;

/// <summary>Manual smoke dump; not part of regression but useful for inspection.</summary>
public class SmokeDump
{
    private readonly ITestOutputHelper _output;

    public SmokeDump(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Dump_Cmaj7_DADABE_first_voicing()
    {
        var json = TestHelpers.RunVoicings("Cmaj7", "DADABE", limit: 1, pretty: true);
        _output.WriteLine(json);
    }

    [Fact]
    public void Dump_chord_FSharpMaj7()
    {
        var json = TestHelpers.RunChord("F#maj7");
        _output.WriteLine(json);
    }
}
