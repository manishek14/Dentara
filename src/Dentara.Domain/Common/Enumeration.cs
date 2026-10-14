namespace Dentara.Domain.Common;

/// <summary>
/// Enumeration base class for strongly-typed enums with explicit values
/// stored in the database. Avoids magic strings for status fields.
/// </summary>
public abstract class Enumeration : IComparable
{
    public int Value { get; }

    public string Name { get; }

    protected Enumeration(int value, string name)
    {
        Value = value;
        Name = name;
    }

    public override string ToString() => Name;

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration other) return false;
        if (GetType() != other.GetType()) return false;
        return Value == other.Value;
    }

    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(object? other) =>
        other is Enumeration e ? Value.CompareTo(e.Value) : 1;

    public static bool operator ==(Enumeration? left, Enumeration? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Enumeration? left, Enumeration? right) => !(left == right);

    protected static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.Static |
                            System.Reflection.BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .Cast<T>();

    protected static T FromValue<T>(int value, string paramName = "value") where T : Enumeration
    {
        var matching = GetAll<T>().FirstOrDefault(e => e.Value == value);
        if (matching is null)
            throw new ArgumentException($"No {typeof(T).Name} with value {value}.", paramName);
        return matching;
    }

    protected static T FromName<T>(string name, string paramName = "name") where T : Enumeration
    {
        var matching = GetAll<T>().FirstOrDefault(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (matching is null)
            throw new ArgumentException($"No {typeof(T).Name} with name '{name}'.", paramName);
        return matching;
    }
}
