namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Two libraries that call each other, as the libraries a FORTRAN program
/// is linked from do. The results the tests expect are those the reference
/// compiler gives for the two linked into one program.
/// </summary>
internal static class LibrariesSample
{
    public const string Core =
        """
              SUBROUTINE CPUT ( N )
              INTEGER N, TOTAL
              SAVE TOTAL
              DATA TOTAL / 0 /
              TOTAL = TOTAL + N
              RETURN

              ENTRY CGET ( N )
              N = TOTAL
              END

              SUBROUTINE COPEN ( NAME, UNIT )
              CHARACTER*(*) NAME
              INTEGER UNIT
              OPEN ( UNIT=UNIT, FILE=NAME, STATUS='NEW', ACCESS='DIRECT',
             .       RECL=4 )
              WRITE ( UNIT, REC=1 ) 1234
              END

              INTEGER FUNCTION CAPPLY ( F, N )
              EXTERNAL F
              INTEGER F, N
              CAPPLY = F ( N ) + 1
              END

              INTEGER FUNCTION CTWICE ( N )
              INTEGER N
              CTWICE = 2 * N
              END

              INTEGER FUNCTION CBACK ( N )
              INTEGER N, ATRIPL
              CBACK = ATRIPL ( N ) + 1
              END
        """;

    public const string App =
        """
              INTEGER FUNCTION ATRIPL ( N )
              INTEGER N
              ATRIPL = 3 * N
              END

              SUBROUTINE ARUN ( NAME, OUT )
              CHARACTER*(*) NAME
              INTEGER OUT ( * ), CAPPLY, CBACK, ATRIPL, CTWICE, V
              EXTERNAL ATRIPL, CTWICE
              CALL CPUT ( 5 )
              CALL CPUT ( 7 )
              CALL CGET ( OUT ( 1 ) )
              CALL COPEN ( NAME, 31 )
              READ ( 31, REC=1 ) V
              OUT ( 2 ) = V
              CLOSE ( 31, STATUS='DELETE' )
              OUT ( 3 ) = CAPPLY ( ATRIPL, 4 )
              OUT ( 4 ) = CAPPLY ( CTWICE, 4 )
              OUT ( 5 ) = CBACK ( 2 )
              END
        """;
}
