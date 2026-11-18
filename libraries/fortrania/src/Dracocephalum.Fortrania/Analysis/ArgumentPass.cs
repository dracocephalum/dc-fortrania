namespace Dracocephalum.Fortrania.Analysis;

/// <summary>One place a procedure passes a dummy argument of its own on to a procedure of another program unit.</summary>
/// <param name="Procedure">The procedure it is passed to, in upper case.</param>
/// <param name="Index">Which of that procedure's arguments it becomes, counted from zero.</param>
public readonly record struct ArgumentPass(string Procedure, int Index);
