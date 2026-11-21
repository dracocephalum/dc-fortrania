namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that define statement functions. The results the tests
/// expect of them are those the reference compiler gives for the same text.
/// </summary>
internal static class StatementFunctionSample
{
    public const string Source =
        """
              INTEGER FUNCTION TWICE ( M )
              INTEGER M
              TWICE = M * 2
              END

              INTEGER FUNCTION SFUNCS ( N, K, OUT )
              INTEGER N, K, OUT ( * )
              INTEGER I, J, BASE, TABLE ( 3 )
              DOUBLE PRECISION X, HALF, SCALED
              LOGICAL ODD, P
              INTEGER CUBE, SHIFT, PICK, BOTH, VIA
              INTEGER TWICE, CODE
              LOGICAL EITHER, Q
              CHARACTER*1 CH
              CHARACTER*4 NAME
              SAVE BASE
              DATA BASE / 100 /
              CUBE ( I ) = I * I * I
              ODD ( I ) = MOD ( I, 2 ) .NE. 0
              HALF ( X ) = X / 2
              SHIFT ( I ) = I + BASE + K
              PICK ( I, J ) = TABLE ( I ) * J + N
              BOTH ( I ) = CUBE ( I ) + SHIFT ( I )
              VIA ( I ) = TWICE ( I ) + TWICE ( I + 1 )
              SCALED ( X, I ) = HALF ( X ) * I
              EITHER ( P, Q ) = P .OR. .NOT. Q
              CODE ( CH ) = ICHAR ( CH ) - MAX ( -1, MIN ( 0, ICHAR ( CH ) ) )

              I = 7
              TABLE ( 1 ) = 10
              TABLE ( 2 ) = 20
              TABLE ( 3 ) = 30
              OUT ( 1 ) = CUBE ( N )
              OUT ( 2 ) = CUBE ( N + 1 )
              P = ODD ( N )
              OUT ( 3 ) = 0
              IF ( P ) OUT ( 3 ) = 1
              IF ( ODD ( N + 1 ) ) OUT ( 3 ) = OUT ( 3 ) + 10
              OUT ( 4 ) = INT ( HALF ( 7.0D0 ) * 10 )
              OUT ( 5 ) = SHIFT ( 5 )
              OUT ( 6 ) = PICK ( 2, 3 )
              OUT ( 7 ) = BOTH ( 2 )
              OUT ( 8 ) = VIA ( 4 )
              OUT ( 9 ) = INT ( SCALED ( 9.0D0, 3 ) * 10 )
              OUT ( 10 ) = I
              OUT ( 11 ) = 0
              IF ( EITHER ( N .GT. 5, ODD ( N + 1 ) ) ) OUT ( 11 ) = 1
              IF ( EITHER ( .FALSE., P ) ) OUT ( 11 ) = OUT ( 11 ) + 10
              NAME = 'WXYZ'
              OUT ( 12 ) = CODE ( 'A' ) + CODE ( NAME ( 2:2 ) )
              BASE = BASE + 1
              SFUNCS = CUBE ( CUBE ( 2 ) )
              END

              SUBROUTINE SFA ( A, R )
              INTEGER A, B, R, I, SQ
              SQ ( I ) = I * I + 1
              R = SQ ( A )
              RETURN

              ENTRY SFB ( B, R )
              R = SQ ( B ) + SQ ( B + 1 )
              RETURN
              END
        """;
}
