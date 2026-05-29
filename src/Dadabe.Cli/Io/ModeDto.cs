namespace Dadabe.Cli.Io
{
    // Placeholder DTO for musical modes
    public record ModeDto(string Name, string? ParentScale, int DegreeIndex, int[] Intervals, string[]? NoteNames = null, string? Description = null);
}
