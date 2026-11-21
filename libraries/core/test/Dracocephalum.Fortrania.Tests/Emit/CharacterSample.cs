namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that between them use what the emitter translates of
/// character data. The results the tests expect of them are those the
/// reference compiler gives for the same text.
/// </summary>
internal static class CharacterSample
{
    public const string Source =
        """
              SUBROUTINE CASSGN ( SHORT, LONG, OUT )
              IMPLICIT NONE
              CHARACTER*(*) SHORT, LONG, OUT
              CHARACTER*4 FOUR
              CHARACTER*10 TEN
              FOUR = LONG
              TEN = SHORT
              OUT = TEN
              OUT ( 12:15 ) = FOUR
              OUT ( 17: ) = 'TAIL'
              OUT ( :1 ) = '>'
              OUT ( 22:30 ) = 'A''B"C\D'
              END

              INTEGER FUNCTION CCMP ( A, B )
              IMPLICIT NONE
              CHARACTER*(*) A, B
              CCMP = 0
              IF ( A .EQ. B ) CCMP = CCMP + 1
              IF ( A .NE. B ) CCMP = CCMP + 2
              IF ( A .LT. B ) CCMP = CCMP + 4
              IF ( A .LE. B ) CCMP = CCMP + 8
              IF ( A .GT. B ) CCMP = CCMP + 16
              IF ( A .GE. B ) CCMP = CCMP + 32
              IF ( LGE ( A, B ) ) CCMP = CCMP + 64
              IF ( LLT ( A, B ) ) CCMP = CCMP + 128
              IF ( LGT ( A, B ) .OR. LLE ( A, B ) ) CCMP = CCMP + 256
              IF ( A ( 1:1 ) .EQ. 'A' ) CCMP = CCMP + 512
              END

              SUBROUTINE CINTR ( S, K, OUT )
              IMPLICIT NONE
              CHARACTER*(*) S
              INTEGER K, OUT ( 10 )
              CHARACTER*3 LOC
              CHARACTER*5 ARR ( 4 )
              LOC = CHAR ( K )
              ARR ( 2 ) = S
              OUT ( 1 ) = LEN ( S )
              OUT ( 2 ) = LEN ( LOC )
              OUT ( 3 ) = LEN ( ARR )
              OUT ( 4 ) = INDEX ( S, 'LO' )
              OUT ( 5 ) = INDEX ( S, 'ZZ' )
              OUT ( 6 ) = ICHAR ( S ( 2:2 ) )
              OUT ( 7 ) = ICHAR ( LOC ( 1:1 ) ) + 1000 * ICHAR ( LOC ( 3:3 ) )
              OUT ( 8 ) = LEN ( S ( 2:K - 60 ) )
              OUT ( 9 ) = INDEX ( S ( 3: ), 'L' )
             .          + 10 * INDEX ( ARR ( 2 ), 'O' )
              OUT ( 10 ) = ICHAR ( ARR ( 2 ) ( K - 61:K - 61 ) )
              END

              SUBROUTINE CFILL ( N, NAMES, VALUE )
              IMPLICIT NONE
              INTEGER N, I
              CHARACTER*(*) NAMES ( N ), VALUE
              DO I = 1, N
                 NAMES ( I ) = VALUE
              END DO
              NAMES ( N ) ( 2:3 ) = '??'
              END

              SUBROUTINE CFIXED ( C, N )
              IMPLICIT NONE
              CHARACTER*3 C
              INTEGER N
              N = LEN ( C )
              C = 'abcdef'
              END

              SUBROUTINE CCALL ( OUT, N )
              IMPLICIT NONE
              CHARACTER*(*) OUT
              INTEGER N
              CHARACTER*6 LIST ( 3 )
              CHARACTER*8 WIDE
              CHARACTER*2 TAG
              PARAMETER ( TAG = 'ab' )
              INTEGER CCMP, I
              CALL CFILL ( 3, LIST, 'xyz' )
              CALL CFILL ( 1, LIST ( 2 ), TAG )
              CALL CFILL ( 1, LIST ( 1 ) ( 4:6 ), 'Q' )
              WIDE = '12345678'
              CALL CFIXED ( WIDE, N )
              OUT = LIST ( 1 )
              DO I = 2, 3
                 OUT ( 6 * I - 5:6 * I ) = LIST ( I )
              END DO
              OUT ( 19:26 ) = WIDE
              OUT ( 27:27 ) = CHAR ( 48 + CCMP ( LIST ( 3 ), 'x??' ) / 100 )
              N = N + 10 * CCMP ( TAG, 'ab ' ) + 10000 * CCMP ( 'b', TAG )
              END
        """;
}
