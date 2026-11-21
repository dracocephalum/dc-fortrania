namespace Dracocephalum.Fortrania.Syntax;

/// <summary>What kind of statement a statement is, as far as its own text can say.</summary>
public enum StatementKind
{
    /// <summary>A statement with no tokens, such as a line that holds only a label.</summary>
    Empty,

    /// <summary>An assignment, or a statement function definition, which looks the same.</summary>
    Assignment,

    /// <summary><c>PROGRAM</c>.</summary>
    Program,

    /// <summary><c>SUBROUTINE</c>.</summary>
    Subroutine,

    /// <summary><c>FUNCTION</c>, with or without a type in front.</summary>
    Function,

    /// <summary><c>ENTRY</c>.</summary>
    Entry,

    /// <summary><c>BLOCK DATA</c>.</summary>
    BlockData,

    /// <summary>A type statement: <c>INTEGER</c>, <c>REAL</c>, <c>DOUBLE PRECISION</c>, <c>COMPLEX</c>, <c>LOGICAL</c> or <c>CHARACTER</c>.</summary>
    TypeDeclaration,

    /// <summary><c>IMPLICIT</c>, including <c>IMPLICIT NONE</c>.</summary>
    Implicit,

    /// <summary><c>PARAMETER</c>.</summary>
    Parameter,

    /// <summary><c>DIMENSION</c>.</summary>
    Dimension,

    /// <summary><c>COMMON</c>.</summary>
    Common,

    /// <summary><c>EQUIVALENCE</c>.</summary>
    Equivalence,

    /// <summary><c>EXTERNAL</c>.</summary>
    External,

    /// <summary><c>INTRINSIC</c>.</summary>
    Intrinsic,

    /// <summary><c>SAVE</c>.</summary>
    Save,

    /// <summary><c>DATA</c>.</summary>
    Data,

    /// <summary><c>INCLUDE</c>.</summary>
    Include,

    /// <summary>A logical <c>IF</c>: a condition and one statement.</summary>
    LogicalIf,

    /// <summary>An arithmetic <c>IF</c>: an expression and three labels.</summary>
    ArithmeticIf,

    /// <summary>A block <c>IF</c>, ending in <c>THEN</c>.</summary>
    BlockIf,

    /// <summary><c>ELSE IF</c>.</summary>
    ElseIf,

    /// <summary><c>ELSE</c>.</summary>
    Else,

    /// <summary><c>END IF</c>.</summary>
    EndIf,

    /// <summary>A counted <c>DO</c>.</summary>
    Do,

    /// <summary><c>DO WHILE</c>.</summary>
    DoWhile,

    /// <summary><c>END DO</c>.</summary>
    EndDo,

    /// <summary><c>CONTINUE</c>.</summary>
    Continue,

    /// <summary><c>CALL</c>.</summary>
    Call,

    /// <summary><c>RETURN</c>.</summary>
    Return,

    /// <summary><c>STOP</c>.</summary>
    Stop,

    /// <summary><c>PAUSE</c>.</summary>
    Pause,

    /// <summary><c>GO TO</c>, in any of its three forms.</summary>
    GoTo,

    /// <summary><c>ASSIGN</c> of a label to a variable.</summary>
    AssignLabel,

    /// <summary><c>END</c> of a program unit.</summary>
    End,

    /// <summary><c>READ</c>.</summary>
    Read,

    /// <summary><c>WRITE</c>.</summary>
    Write,

    /// <summary><c>PRINT</c>.</summary>
    Print,

    /// <summary><c>OPEN</c>.</summary>
    Open,

    /// <summary><c>CLOSE</c>.</summary>
    Close,

    /// <summary><c>INQUIRE</c>.</summary>
    Inquire,

    /// <summary><c>REWIND</c>.</summary>
    Rewind,

    /// <summary><c>BACKSPACE</c>.</summary>
    Backspace,

    /// <summary><c>ENDFILE</c>.</summary>
    EndFile,

    /// <summary><c>FORMAT</c>.</summary>
    Format,
}
