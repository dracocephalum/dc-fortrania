namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units with more than one way in. The results the tests expect of
/// them are those the reference compiler gives for the same text.
/// </summary>
internal static class EntrySample
{
    public const string Source =
        """
              SUBROUTINE STACK ( ITEM, COUNT )
              INTEGER ITEM, COUNT
              INTEGER MAXN
              PARAMETER ( MAXN = 10 )
              INTEGER ITEMS ( MAXN ), N, I
              SAVE ITEMS, N
              DATA N / 0 /
              RETURN

              ENTRY PUSH ( ITEM )
              N = N + 1
              ITEMS(N) = ITEM
              RETURN

              ENTRY POP ( ITEM )
              ITEM = ITEMS(N)
              N = N - 1
              RETURN

              ENTRY DEPTH ( COUNT )
              COUNT = 0
              DO I = 1, N
                 COUNT = COUNT + 1
              END DO
              RETURN
              END

              DOUBLE PRECISION FUNCTION AREA ( R )
              DOUBLE PRECISION R, S, SQUARE, SETK, K
              SAVE K
              DATA K / 1.0D0 /
              AREA = K * 3.0D0 * R * R
              RETURN

              ENTRY SQUARE ( S )
              AREA = K * S * S
              RETURN

              ENTRY SETK ( S )
              K = S
              SETK = K + 0.5D0
              END

              SUBROUTINE TWICE ( X )
              INTEGER X, T
              T = 2
              X = X * T
              ENTRY ONCE ( X )
              X = X + 1
              RETURN
              END

              SUBROUTINE FIRST ( A, FLAG )
              INTEGER A, B
              LOGICAL FLAG
              CHARACTER*(*) NAME
              A = 1
              RETURN

              ENTRY OTHER ( B, NAME, FLAG )
              IF ( FLAG ) THEN
                 A = 5
              END IF
              B = 2 + LEN ( NAME )
              RETURN

              ENTRY THIRD ( A, FLAG )
              IF ( FLAG ) THEN
                 B = 7
                 NAME = 'X'
                 A = B + LEN ( NAME )
                 RETURN
              END IF
              A = 3
              RETURN
              END

              CHARACTER*(*) FUNCTION GREET ( WHO )
              CHARACTER*(*) WHO, FAREWL
              GREET = 'HELLO ' // WHO
              RETURN

              ENTRY FAREWL ( WHO )
              FAREWL = 'BYE ' // WHO
              RETURN
              END
        """;
}
