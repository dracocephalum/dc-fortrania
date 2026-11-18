namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that pass procedures as arguments and call the ones they
/// are passed. The results the tests expect of them are those the reference
/// compiler gives for the same text.
/// </summary>
internal static class ProcedureArgumentSample
{
    public const string Source =
        """
              DOUBLE PRECISION FUNCTION SQUARE ( X )
              DOUBLE PRECISION X
              SQUARE = X * X
              END

              DOUBLE PRECISION FUNCTION CUBED ( X )
              DOUBLE PRECISION X
              CUBED = X * X * X
              END

              SUBROUTINE BUMP ( N )
              INTEGER N
              N = N + 1
              END

              LOGICAL FUNCTION ISBIG ( N )
              INTEGER N
              ISBIG = N .GT. 10
              END

              CHARACTER*(*) FUNCTION TAG ( N )
              INTEGER N
              IF ( N .GT. 0 ) THEN
                 TAG = 'POS'
              ELSE
                 TAG = 'NEG'
              END IF
              END

              DOUBLE PRECISION FUNCTION APPLY ( F, X )
              EXTERNAL F
              DOUBLE PRECISION F, X
              APPLY = F ( X ) + F ( X + 1.0D0 )
              END

              DOUBLE PRECISION FUNCTION RELAY ( F, X )
              EXTERNAL F
              DOUBLE PRECISION X, APPLY
              RELAY = APPLY ( F, X )
              END

              SUBROUTINE TIMES ( S, COUNT, N )
              EXTERNAL S
              INTEGER COUNT, N, I
              DO I = 1, COUNT
                 CALL S ( N )
              END DO
              END

              INTEGER FUNCTION COUNTS ( P, T, N )
              EXTERNAL P, T
              LOGICAL P
              CHARACTER*3 T, W
              INTEGER N
              COUNTS = 0
              IF ( P ( N ) ) COUNTS = 1
              W = T ( N )
              IF ( W .EQ. 'POS' ) COUNTS = COUNTS + 10
              END

              DOUBLE PRECISION FUNCTION TWOWAY ( F, X, N )
              EXTERNAL F
              DOUBLE PRECISION F, X
              INTEGER N
              TWOWAY = F ( X )
              IF ( N .LT. 0 ) TWOWAY = F ( N )
              END

              SUBROUTINE DRIVER ( OUT, IOUT )
              EXTERNAL SQUARE, CUBED, BUMP, ISBIG, TAG
              DOUBLE PRECISION SQUARE, CUBED, APPLY, RELAY, TWOWAY, OUT ( * )
              LOGICAL ISBIG
              CHARACTER*3 TAG
              INTEGER COUNTS, IOUT ( * )
              OUT ( 1 ) = APPLY ( SQUARE, 2.0D0 )
              OUT ( 2 ) = APPLY ( CUBED, 2.0D0 )
              OUT ( 3 ) = RELAY ( SQUARE, 3.0D0 )
              OUT ( 4 ) = TWOWAY ( CUBED, 2.0D0, 1 )
              IOUT ( 1 ) = 5
              CALL TIMES ( BUMP, 3, IOUT ( 1 ) )
              IOUT ( 2 ) = COUNTS ( ISBIG, TAG, 11 )
              IOUT ( 3 ) = COUNTS ( ISBIG, TAG, -4 )
              END
        """;
}
