namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that keep variables between calls and initialize them.
/// The results the tests expect of them are those the reference compiler
/// gives for the same text.
/// </summary>
internal static class SavedStateSample
{
    public const string Source =
        """
              SUBROUTINE BUMP ( N, BY )
              IMPLICIT NONE
              INTEGER N, BY
              N = N + BY
              END

              INTEGER FUNCTION COUNTR ( RESET )
              IMPLICIT NONE
              LOGICAL RESET
              INTEGER N, STEPS ( 2 )
              SAVE N
              DATA N / 10 /, STEPS / 1, 4 /
              IF ( RESET ) N = 0
              CALL BUMP ( N, STEPS ( 1 ) )
              CALL BUMP ( STEPS ( 1 ), STEPS ( 2 ) )
              COUNTR = N
              END

              SUBROUTINE TABLES ( OUT )
              IMPLICIT NONE
              INTEGER OUT ( 12 )
              INTEGER PRIMES ( 6 ), GRID ( 2, 3 ), I, J, MANY ( 100 ), ODD ( 5 )
              INTEGER K, PAIRS ( 2, 2 )
              DOUBLE PRECISION D ( 3 ), X
              REAL R
              LOGICAL L ( 3 ), FIRST
              CHARACTER*4 NAMES ( 3 ), TAG, FILL ( 6 )
              CHARACTER*6 SUB
              SAVE
              DATA PRIMES / 2, 3, 5, 7, 11, 13 /
              DATA GRID / 1, 2, 3, 4, 5, 6 /
              DATA MANY / 40 * 7, 60 * 0 /
              DATA ( ODD ( I ), I = 1, 5, 2 ) / 3 * 9 /
              DATA ( ( PAIRS ( I, J ), J = 1, 2 ), I = 1, 2 ) / 11, 12, 21, 22 /
              DATA D / 1.5D0, 2 * -2.5D0 /, X, R / 0.1, 0.25D0 /
              DATA L / .TRUE., .FALSE., .TRUE. /, FIRST / .TRUE. /
              DATA NAMES / 'ab', 'cdefgh', ' ' /, TAG / 'T' /
              DATA SUB ( 2:4 ) / 'xyz' /, K / 0 /
              DATA FILL / 5 * 'same', 'last' /
              OUT ( 1 ) = PRIMES ( 4 ) + GRID ( 2, 3 ) * 100
              OUT ( 2 ) = MANY ( 40 ) * 10 + MANY ( 41 ) + MANY ( 1 )
              OUT ( 3 ) = ODD ( 1 ) + ODD ( 3 ) * 10 + ODD ( 5 ) * 100
             .          + PAIRS ( 2, 1 ) * 1000 + PAIRS ( 1, 2 ) * 100000
              OUT ( 4 ) = NINT ( D ( 1 ) * 10 ) + NINT ( D ( 3 ) * 10 ) * 100
              OUT ( 5 ) = NINT ( X * 1.0D9 )
              OUT ( 6 ) = NINT ( R * 1000 )
              IF ( FIRST ) THEN
                 OUT ( 7 ) = 1
                 FIRST = .FALSE.
              ELSE
                 OUT ( 7 ) = 2
              END IF
              IF ( L ( 1 ) .AND. .NOT. L ( 2 ) .AND. L ( 3 ) )
             .   OUT ( 7 ) = OUT ( 7 ) + 10
              OUT ( 8 ) = ICHAR ( NAMES ( 2 ) ( 4:4 ) )
             .          + 1000 * ICHAR ( NAMES ( 1 ) ( 3:3 ) )
              OUT ( 9 ) = ICHAR ( TAG ( 2:2 ) ) + ICHAR ( NAMES ( 3 ) ( 1:1 ) )
              SUB ( 1:1 ) = '<'
              SUB ( 5:6 ) = '>>'
              OUT ( 10 ) = INDEX ( SUB, 'xyz' ) * 1000 + ICHAR ( SUB ( 4:4 ) )
              K = K + 1
              OUT ( 11 ) = K
              OUT ( 12 ) = 0
              DO I = 1, 6
                 IF ( FILL ( I ) .EQ. 'same' ) OUT ( 12 ) = OUT ( 12 ) + 1
                 IF ( FILL ( I ) .EQ. 'last' ) OUT ( 12 ) = OUT ( 12 ) + 10 * I
              END DO
              END

              DOUBLE PRECISION FUNCTION BIGSUM ( N )
              IMPLICIT NONE
              INTEGER N, I
              DOUBLE PRECISION BIG ( 1000 )
              CHARACTER*5000 PAGE
              DO I = 1, N
                 BIG ( I ) = I
              END DO
              PAGE = 'x'
              PAGE ( 5000:5000 ) = 'y'
              BIGSUM = ICHAR ( PAGE ( 5000:5000 ) )
             .       + ICHAR ( PAGE ( 4999:4999 ) )
              DO I = 1, N
                 BIGSUM = BIGSUM + BIG ( I )
              END DO
              END
        """;
}
