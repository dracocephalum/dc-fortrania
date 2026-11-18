namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that connect files and transfer unformatted records by
/// number. The results the tests expect of them are those the reference
/// compiler gives for the same text.
/// </summary>
internal static class RecordSample
{
    public const string Source =
        """
              SUBROUTINE RECS ( NAME, OUT, WORD, VALS )
              CHARACTER*(*) NAME, WORD
              INTEGER OUT ( * ), UNIT, IOS, I, N, IA ( 4 ), IB ( 4 )
              DOUBLE PRECISION VALS ( 2 ), D ( 2 )
              LOGICAL EX, OP, NMD
              CHARACTER*255 FOUND
              INTEGER RECLEN
              PARAMETER ( RECLEN = 32 )

              UNIT = 11
              INQUIRE ( FILE=NAME, EXIST=EX, OPENED=OP, NUMBER=N, IOSTAT=IOS )
              OUT ( 1 ) = IOS
              IF ( EX ) OUT ( 1 ) = OUT ( 1 ) + 1
              IF ( OP ) OUT ( 1 ) = OUT ( 1 ) + 10
              OUT ( 2 ) = N

              OPEN ( UNIT=UNIT, FILE=NAME, STATUS='NEW', ACCESS='DIRECT',
             .       RECL=RECLEN, IOSTAT=IOS )
              OUT ( 3 ) = IOS

              D ( 1 ) = 1.5D0
              D ( 2 ) = -2.25D0
              DO I = 1, 4
                 IA ( I ) = 6 + I
              END DO
              WRITE ( UNIT, REC=2, IOSTAT=IOS ) 'ABCDEFGH', D, IA ( 2 )
              OUT ( 4 ) = IOS
              WRITE ( UNIT, REC=4 ) ( IA ( I ), I = 4, 1, -1 ),
             .                      D ( 1 ) + D ( 2 )

              READ ( UNIT, REC=2, IOSTAT=IOS ) WORD ( 1:8 ), VALS, OUT ( 5 )
              OUT ( 6 ) = IOS
              READ ( UNIT=UNIT, REC=4 ) IB, VALS ( 2 )
              DO I = 1, 4
                 OUT ( 6 + I ) = IB ( I )
              END DO
              READ ( UNIT, REC=3, IOSTAT=IOS ) IB
              OUT ( 11 ) = IOS
              OUT ( 12 ) = IB ( 1 )
              READ ( UNIT, REC=9, IOSTAT=IOS ) IB
              OUT ( 13 ) = IOS
              READ ( UNIT, REC=2, IOSTAT=IOS ) IA, IA, IA
              OUT ( 14 ) = IOS
              WRITE ( UNIT, REC=5, IOSTAT=IOS ) IA, IA, IA
              OUT ( 15 ) = IOS

              INQUIRE ( UNIT=UNIT, OPENED=OP, NAMED=NMD, NAME=FOUND, NUMBER=N )
              OUT ( 16 ) = 0
              IF ( OP ) OUT ( 16 ) = OUT ( 16 ) + 1
              IF ( NMD ) OUT ( 16 ) = OUT ( 16 ) + 10
              IF ( FOUND .EQ. NAME ) OUT ( 16 ) = OUT ( 16 ) + 100
              OUT ( 17 ) = N
              INQUIRE ( FILE=NAME, EXIST=EX, OPENED=OP, NUMBER=N )
              OUT ( 18 ) = 0
              IF ( EX ) OUT ( 18 ) = OUT ( 18 ) + 1
              IF ( OP ) OUT ( 18 ) = OUT ( 18 ) + 10
              OUT ( 19 ) = N

              CLOSE ( UNIT, STATUS='DELETE', IOSTAT=IOS )
              OUT ( 20 ) = IOS
              INQUIRE ( FILE=NAME, EXIST=EX )
              OUT ( 21 ) = 0
              IF ( EX ) OUT ( 21 ) = 1
              OPEN ( UNIT=UNIT, FILE=NAME, STATUS='OLD', IOSTAT=IOS )
              OUT ( 22 ) = IOS
              INQUIRE ( UNIT=UNIT, OPENED=OP, NUMBER=N )
              OUT ( 23 ) = N
              IF ( OP ) OUT ( 23 ) = 1
              REWIND ( UNIT, IOSTAT=IOS )
              OUT ( 24 ) = IOS
              END

              SUBROUTINE FAILS ( NAME )
              CHARACTER*(*) NAME
              OPEN ( UNIT=12, FILE=NAME, STATUS='OLD' )
              END
        """;
}
