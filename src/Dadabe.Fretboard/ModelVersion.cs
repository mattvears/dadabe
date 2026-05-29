namespace Dadabe.Fretboard;

/// <summary>
/// Governs which schema fields and behaviours the environment activates.
/// Increment when a non-additive schema change lands; version gates in
/// command and mapping code branch on this value so each feature's upgrade
/// path is explicit and mechanical.
/// </summary>
public enum ModelVersion
{
    V1 = 1,
    V2 = 2
}
