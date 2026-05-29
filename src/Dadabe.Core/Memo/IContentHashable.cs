namespace Dadabe.Core.Memo;

/// <summary>
/// A value whose identity is a stable content hash (D17).
/// </summary>
public interface IContentHashable
{
    ContentHash ContentHash { get; }
}
