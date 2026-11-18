namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that join character values and define and reference
/// character functions. The results the tests expect of them are those the
/// reference compiler gives for the same text.
/// </summary>
internal static class ConcatenationSample
{
    public const string Source =
        """
              SUBROUTINE CJOIN ( A, B, OUT )
              IMPLICIT NONE
              CHARACTER*(*) A, B, OUT
              CHARACTER*6 SIX
              CHARACTER*20 WORK
              SIX = A // B
              OUT = SIX // '|' // A ( 2: ) // CHAR ( 33 ) // ( B // '.' )
              WORK = 'abcdefghij'
              WORK = WORK ( 3:6 ) // WORK ( 1:2 ) // 'Z'
              OUT ( 21:40 ) = WORK
              WORK ( 1:8 ) = WORK ( 5:6 ) // WORK ( 1:4 )
              OUT ( 41:48 ) = WORK ( 1:8 )
              SIX = 'k' // SIX
              OUT ( 51:56 ) = SIX
              END

              INTEGER FUNCTION CCOUNT ( S, C )
              IMPLICIT NONE
              CHARACTER*(*) S
              CHARACTER*1 C
              INTEGER I
              CCOUNT = 10 * LEN ( S )
              DO I = 1, LEN ( S )
                 IF ( S ( I:I ) .EQ. C ) CCOUNT = CCOUNT + 1
              END DO
              END

              INTEGER FUNCTION CJARG ( N )
              IMPLICIT NONE
              INTEGER N, CCOUNT
              CHARACTER*5 LOC
              LOC = 'xy'
              CJARG = CCOUNT ( LOC // 'x' // LOC ( 1:N ), 'x' )
             .      + 1000 * LEN ( LOC // 'ab' // LOC ( 2:N ) )
              IF ( LOC ( 1:2 ) // 'z' .EQ. 'xyz' ) CJARG = CJARG + 100000
              IF ( 'a' // LOC .LT. 'b' // LOC ) CJARG = CJARG + 200000
              CJARG = CJARG + 1000000 * INDEX ( LOC // LOC, ' ' // 'x' )
              END

              CHARACTER*(*) FUNCTION UPPER ( S )
              IMPLICIT NONE
              CHARACTER*(*) S
              INTEGER I, K
              UPPER = S
              DO I = 1, MIN ( LEN ( S ), LEN ( UPPER ) )
                 K = ICHAR ( S ( I:I ) )
                 IF ( K .GE. 97 .AND. K .LE. 122 )
             .      UPPER ( I:I ) = CHAR ( K - 32 )
              END DO
              RETURN
              END

              CHARACTER*3 FUNCTION TAG ( N )
              IMPLICIT NONE
              INTEGER N
              TAG = 'no'
              IF ( N .GT. 0 ) TAG = 'yes'
              END

              SUBROUTINE CFUNC ( IN, OUT )
              IMPLICIT NONE
              CHARACTER*(*) IN, OUT
              CHARACTER*8 UPPER, TMP
              CHARACTER*3 TAG
              INTEGER CCOUNT
              TMP = UPPER ( IN )
              OUT = TMP // '/' // UPPER ( 'ab' // TMP ( 1:3 ) ) // TAG ( 1 )
             .   // TAG ( -1 )
              IF ( UPPER ( 'q' ) .EQ. 'Q' .AND.
             .     TAG ( CCOUNT ( UPPER ( IN ), 'A' ) ) .EQ. 'yes' )
             .   OUT ( 30:30 ) = '!'
              OUT ( 32:35 ) = UPPER ( TAG ( 2 ) )
              END
        """;
}
