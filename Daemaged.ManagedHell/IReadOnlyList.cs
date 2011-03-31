namespace Daemaged.ManagedHell
{
  public interface IReadOnlyList<out T>
  {
    int Count { get; }
    T this[int index] { get; }
  }
}
