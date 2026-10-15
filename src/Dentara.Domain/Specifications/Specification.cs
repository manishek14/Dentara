namespace Dentara.Domain.Specifications;

/// <summary>
/// Base class for specifications. Provides And/Or/Not combinators with
/// short-circuit-friendly expression composition.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    public abstract bool IsSatisfiedBy(T candidate);

    public abstract System.Linq.Expressions.Expression<Func<T, bool>> ToExpression();

    public Specification<T> And(ISpecification<T> other) => new AndSpecification<T>(this, other);

    public Specification<T> Or(ISpecification<T> other) => new OrSpecification<T>(this, other);

    public Specification<T> Not() => new NotSpecification<T>(this);

    public static Specification<T> operator &(Specification<T> left, ISpecification<T> right) =>
        new AndSpecification<T>(left, right);

    public static Specification<T> operator |(Specification<T> left, ISpecification<T> right) =>
        new OrSpecification<T>(left, right);

    public static Specification<T> operator !(Specification<T> spec) =>
        new NotSpecification<T>(spec);
}

internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override bool IsSatisfiedBy(T candidate) =>
        _left.IsSatisfiedBy(candidate) && _right.IsSatisfiedBy(candidate);

    public override System.Linq.Expressions.Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T));
        var body = System.Linq.Expressions.Expression.AndAlso(
            System.Linq.Expressions.Expression.Invoke(leftExpr, parameter),
            System.Linq.Expressions.Expression.Invoke(rightExpr, parameter));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public OrSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override bool IsSatisfiedBy(T candidate) =>
        _left.IsSatisfiedBy(candidate) || _right.IsSatisfiedBy(candidate);

    public override System.Linq.Expressions.Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T));
        var body = System.Linq.Expressions.Expression.OrElse(
            System.Linq.Expressions.Expression.Invoke(leftExpr, parameter),
            System.Linq.Expressions.Expression.Invoke(rightExpr, parameter));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _inner;

    public NotSpecification(ISpecification<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public override bool IsSatisfiedBy(T candidate) => !_inner.IsSatisfiedBy(candidate);

    public override System.Linq.Expressions.Expression<Func<T, bool>> ToExpression()
    {
        var innerExpr = _inner.ToExpression();
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T));
        var body = System.Linq.Expressions.Expression.Not(
            System.Linq.Expressions.Expression.Invoke(innerExpr, parameter));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
