namespace Dentara.Domain.Specifications;

/// <summary>
/// Specification pattern for expressing business rules as composable predicates.
/// Used to encapsulate domain rules that would otherwise be scattered across
/// query code. Example: "active appointment that overlaps range X for dentist Y".
///
/// The Application/Infrastructure layers translate specifications to EF Core
/// expressions or raw SQL. The Domain layer stays pure (no EF Core dependency).
/// </summary>
public interface ISpecification<T>
{
    /// <summary>
    /// Returns true if the candidate satisfies the specification.
    /// </summary>
    bool IsSatisfiedBy(T candidate);

    /// <summary>
    /// EF Core translatable expression. The Infrastructure layer uses this to
    /// build WHERE clauses without materializing entities.
    /// </summary>
    System.Linq.Expressions.Expression<Func<T, bool>> ToExpression();
}
