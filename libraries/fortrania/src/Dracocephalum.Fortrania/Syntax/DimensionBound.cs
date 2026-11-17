namespace Dracocephalum.Fortrania.Syntax;

/// <summary>The bounds of one dimension of an array declarator.</summary>
/// <param name="Lower">The lower bound, or <see langword="null"/> when omitted, which means one.</param>
/// <param name="Upper">The upper bound, or <see langword="null"/> for <c>*</c>, an assumed size.</param>
public sealed record DimensionBound(Expression? Lower, Expression? Upper);
