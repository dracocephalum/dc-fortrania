namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that write and read text by formats. What the tests
/// expect of them is what the reference compiler's program wrote for the
/// same text, line for line; each expected line is followed here by a bar,
/// so that the blanks a line ends in can be seen.
/// </summary>
internal static class FormattedSample
{
    public const string Source =
        """
              SUBROUTINE FMTOUT ( NAME )
              CHARACTER*(*) NAME
              INTEGER I, J, K ( 3 ), U
              DOUBLE PRECISION D, E ( 2 )
              REAL R
              LOGICAL L, M ( 2 )
              CHARACTER*5 C
              CHARACTER*3 S ( 2 )
              CHARACTER*40 F
              U = 21
              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW' )
              I = 42
              J = -7
              K ( 1 ) = 1
              K ( 2 ) = 22
              K ( 3 ) = 333
              D = 1.5D0
              E ( 1 ) = -0.000123456789D0
              E ( 2 ) = 6.02214076D23
              R = 3.14159
              L = .TRUE.
              M ( 1 ) = .FALSE.
              M ( 2 ) = .TRUE.
              C = 'AB'
              S ( 1 ) = 'xyz'
              S ( 2 ) = 'uvw'
              WRITE ( U, '(A)' ) '[' // C // ']'
              WRITE ( U, '(A,A)' ) C, '|'
              WRITE ( U, '(A2,A7,A)' ) C, C, '|'
              WRITE ( U, '(1X,A)' ) C
              WRITE ( U, '(1X,3A)' ) C, C, C
              WRITE ( U, '(1X,A,D25.17,A)' ) 'D=', D, '|'
              WRITE ( U, '(D25.17)' ) E ( 1 )
              WRITE ( U, '(D25.17)' ) E ( 2 )
              WRITE ( U, '(D25.17)' ) 0.0D0
              WRITE ( U, '(D25.17)' ) -D
              WRITE ( U, '(D10.3)' ) 1.0D-100
              WRITE ( U, '(D10.3)' ) 1.0D+123
              WRITE ( U, '(D8.3)' ) -D
              WRITE ( U, '(D7.3)' ) -D
              WRITE ( U, '(2D12.4)' ) E
              WRITE ( U, '(E26.17)' ) E ( 2 )
              WRITE ( U, '(E12.4)' ) R
              WRITE ( U, '(E12.4E3)' ) R
              WRITE ( U, '(E12.4E1)' ) E ( 2 )
              WRITE ( U, '(1PE24.16)' ) E ( 2 )
              WRITE ( U, '(1P,E12.4,E12.4)' ) D, E ( 1 )
              WRITE ( U, '(2PE12.4)' ) D
              WRITE ( U, '(-2PE12.4)' ) D
              WRITE ( U, '(E12.4)' ) 9.99996D0
              WRITE ( U, '(F20.1)' ) E ( 1 )
              WRITE ( U, '(F18.13)' ) E ( 1 )
              WRITE ( U, '(F8.3)' ) R
              WRITE ( U, '(F8.3)' ) -0.0004D0
              WRITE ( U, '(F5.1)' ) 0.05D0
              WRITE ( U, '(F5.1)' ) 0.25D0
              WRITE ( U, '(F5.1)' ) 0.35D0
              WRITE ( U, '(F5.1)' ) 0.75D0
              WRITE ( U, '(F5.1)' ) 0.0006D0
              WRITE ( U, '(F4.1)' ) 12345.6D0
              WRITE ( U, '(F6.0)' ) 2.5D0
              WRITE ( U, '(F6.0)' ) 3.5D0
              WRITE ( U, '(F6.0)' ) 0.5D0
              WRITE ( U, '(F6.0)' ) 0.0D0
              WRITE ( U, '(F6.2)' ) 9.996D0
              WRITE ( U, '(F6.2)' ) 0.0D0
              WRITE ( U, '(F4.2)' ) 0.5D0
              WRITE ( U, '(F3.2)' ) 0.5D0
              WRITE ( U, '(F3.2)' ) 0.0D0
              WRITE ( U, '(F5.2)' ) -0.5D0
              WRITE ( U, '(F4.2)' ) -0.5D0
              WRITE ( U, '(F12.2)' ) 123456789.125D0
              WRITE ( U, '(I10)' ) I
              WRITE ( U, '(I4.4)' ) I
              WRITE ( U, '(I5.3)' ) J
              WRITE ( U, '(I2)' ) K ( 3 )
              WRITE ( U, '(I2)' ) J
              WRITE ( U, '(I1)' ) J
              WRITE ( U, '(I5.0)' ) 0
              WRITE ( U, '(I12)' ) -2147483647 - 1
              WRITE ( U, '(1X,2I10)' ) I, J
              WRITE ( U, '(3I5)' ) K
              WRITE ( U, '(2I5)' ) K
              WRITE ( U, '(2I5)' ) ( K ( I ) + 1, I = 3, 1, -1 )
              I = 42
              WRITE ( U, '(I3,2(1X,A3))' ) I, S
              WRITE ( U, '(I3,2(1X,A3),I4)' ) I, S, J, S, J
              WRITE ( U, '(''N='',I3,'' it''''s'')' ) I
              WRITE ( U, '(A,/,A)' ) 'one', 'two'
              WRITE ( U, '(A,2/,A)' ) 'one', 'three'
              WRITE ( U, '(A,:,''x'',A)' ) 'only'
              WRITE ( U, '(A,''tail'')' ) 'head'
              WRITE ( U, '(A,3X)' ) 'pad'
              WRITE ( U, '(3X,A,2X,A)' ) 'a', 'b'
              WRITE ( U, '(L1,L4)' ) L, .FALSE.
              WRITE ( U, '(2L2)' ) M
              WRITE ( U, '(A)' )
              WRITE ( U, '(''alone'')' )
              WRITE ( U, '(A,$)' ) 'no newline'
              WRITE ( U, '(A)' ) '<'
              F = '(A,I2)'
              WRITE ( U, F ) 'var fmt', I
              WRITE ( U, '(A, I 4 )' ) 'spaces', I
              WRITE ( U, '(a,i4)' ) 'lower', I
              WRITE ( U, '(A)' ) S
              WRITE ( U, FMT='(A,I3)' ) 'keyword', I
              CLOSE ( U )
              END

              SUBROUTINE FMTIN ( NAME, LOG )
              CHARACTER*(*) NAME, LOG
              INTEGER IOS, N, U, P
              CHARACTER*8 LINE
              CHARACTER*3 A, B
              CHARACTER*12 RAW
              CHARACTER*40 F
              DOUBLE PRECISION D
              REAL R
              LOGICAL L

              U = 22
              P = 23
              OPEN ( UNIT=P, FILE=LOG, STATUS='NEW' )
              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW', IOSTAT=IOS )
              WRITE ( U, '(A)', IOSTAT=IOS ) 'first line is long'
              WRITE ( U, '(A)', IOSTAT=IOS ) 'two'
              WRITE ( U, '(A)', IOSTAT=IOS )
              WRITE ( U, '(A)', IOSTAT=IOS ) 'four'
              WRITE ( P, '(A,I5)' ) 'write', IOS
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5)' ) 'read after write', IOS
              REWIND ( U )
              LINE = '????????'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'long', IOS, '[', LINE, ']'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'short', IOS, '[', LINE, ']'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'empty', IOS, '[', LINE, ']'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'last', IOS, '[', LINE, ']'
              LINE = 'untouchd'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'eof', IOS, '[', LINE, ']'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5)' ) 'eof again', IOS
              WRITE ( U, '(A)', IOSTAT=IOS ) 'five'
              WRITE ( P, '(A,I5)' ) 'write at eof', IOS
              REWIND ( U )
              READ ( U, '(A2,A)', IOSTAT=IOS ) A, B
              WRITE ( P, '(A,I5,5A)' ) 'a2 a', IOS, '[', A, '][', B, ']'
              READ ( U, '(A5,A2)', IOSTAT=IOS ) A, B
              WRITE ( P, '(A,I5,5A)' ) 'a5 past end', IOS, '[', A, '][', B, ']'
              REWIND ( U )
              READ ( U, '(2X,A,/,A)', IOSTAT=IOS ) A, B
              WRITE ( P, '(A,I5,5A)' ) 'x and slash', IOS, '[', A, '][', B, ']'
              REWIND ( U )
              READ ( U, '(A)', IOSTAT=IOS ) A, B
              WRITE ( P, '(A,I5,5A)' ) 'reversion', IOS, '[', A, '][', B, ']'
              READ ( U, '(A)', IOSTAT=IOS )
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'after skip', IOS, '[', LINE, ']'
              REWIND ( U )
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( U, '(A)', IOSTAT=IOS ) 'cut'
              WRITE ( P, '(A,I5)' ) 'write in middle', IOS
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5)' ) 'read after it', IOS
              REWIND ( U )
              N = 0
              IOS = 0
              DO WHILE ( IOS .EQ. 0 )
                 READ ( U, '(A)', IOSTAT=IOS ) LINE
                 IF ( IOS .EQ. 0 ) N = N + 1
              END DO
              WRITE ( P, '(A,I5)' ) 'lines after cut', N
              CLOSE ( U, STATUS='DELETE' )

              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW', ACCESS='DIRECT',
             .       RECL=12 )
              RAW = 'ab' // CHAR ( 10 ) // 'cd' // CHAR ( 13 ) // CHAR ( 10 )
             .   // 'noend'
              WRITE ( U, REC=1 ) RAW
              CLOSE ( U )
              OPEN ( UNIT=U, FILE=NAME, STATUS='OLD' )
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'lf', IOS, '[', LINE, ']'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'crlf', IOS, '[', LINE, ']'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5,3A)' ) 'no ending', IOS, '[', LINE, ']'
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5)' ) 'then', IOS
              CLOSE ( U, STATUS='DELETE' )

              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW', ACCESS='DIRECT',
             .       RECL=8 )
              WRITE ( U, '(A)', IOSTAT=IOS ) 'x'
              WRITE ( P, '(A,I5)' ) 'formatted write on direct', IOS
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5)' ) 'formatted read on direct', IOS
              CLOSE ( U, STATUS='DELETE' )
              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW', FORM='UNFORMATTED' )
              WRITE ( U, '(A)', IOSTAT=IOS ) 'x'
              WRITE ( P, '(A,I5)' ) 'formatted on unformatted', IOS
              CLOSE ( U, STATUS='DELETE' )
              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW' )
              READ ( U, '(A)', IOSTAT=IOS ) LINE
              WRITE ( P, '(A,I5)' ) 'read empty file', IOS

              F = '(I4)'
              WRITE ( U, F, IOSTAT=IOS ) 'XY'
              WRITE ( P, '(A,I5)' ) 'I with character', IOS
              F = '(F8.3)'
              WRITE ( U, F, IOSTAT=IOS ) N
              WRITE ( P, '(A,I5)' ) 'F with integer', IOS
              F = '(I4)'
              WRITE ( U, F, IOSTAT=IOS ) D
              WRITE ( P, '(A,I5)' ) 'I with double', IOS
              F = '(A'
              WRITE ( U, F, IOSTAT=IOS ) 'XY'
              WRITE ( P, '(A,I5)' ) 'bad format', IOS
              F = '(Q4)'
              WRITE ( U, F, IOSTAT=IOS ) 'XY'
              WRITE ( P, '(A,I5)' ) 'unknown descriptor', IOS
              CLOSE ( U, STATUS='DELETE' )

              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW' )
              WRITE ( U, '(A)' ) '  12 -3   '
              WRITE ( U, '(A)' ) ' 12345  1.5 2.5E2-1.5D-2  3+2'
              WRITE ( U, '(A)' ) ' T.FALSE.  F'
              WRITE ( U, '(A)' ) 'x1'
              WRITE ( U, '(A)' ) '12'
              REWIND ( U )
              READ ( U, '(I4,I3,I3)', IOSTAT=IOS ) N, IOS, IOS
              WRITE ( P, '(A,2I6)' ) 'integers', N, IOS
              READ ( U, '(F6.2)', IOSTAT=IOS ) D
              WRITE ( P, '(A,I5,F12.4)' ) 'implied point', IOS, D
              REWIND ( U )
              READ ( U, '(A)' ) LINE
              READ ( U, '(6X,F5.2)', IOSTAT=IOS ) D
              WRITE ( P, '(A,I5,F12.4)' ) 'own point', IOS, D
              REWIND ( U )
              READ ( U, '(A)' ) LINE
              READ ( U, '(11X,E6.1)', IOSTAT=IOS ) D
              WRITE ( P, '(A,I5,F12.4)' ) 'exponent', IOS, D
              REWIND ( U )
              READ ( U, '(A)' ) LINE
              READ ( U, '(17X,D7.0,F5.0)', IOSTAT=IOS ) D, R
              WRITE ( P, '(A,I5,F12.4,F12.4)' ) 'd and sign', IOS, D, R
              READ ( U, '(L2,L7,L3)', IOSTAT=IOS ) L
              WRITE ( P, '(A,I5,L2)' ) 'logical', IOS, L
              READ ( U, '(I2)', IOSTAT=IOS ) N
              WRITE ( P, '(A,I5)' ) 'bad integer', IOS
              READ ( U, '(F2.0)', IOSTAT=IOS ) D
              WRITE ( P, '(A,I5,F8.2)' ) 'f reads whole', IOS, D
              CLOSE ( U, STATUS='DELETE' )
              CLOSE ( P )
              END
        """;

    public const string ExpectedOutput =
        """
        [AB   ]|
        AB   ||
        AB  AB   ||
         AB   |
         AB   AB   AB   |
         D=  0.15000000000000000D+01||
         -0.12345678900000000D-03|
          0.60221407599999999D+24|
          0.00000000000000000D+00|
         -0.15000000000000000D+01|
         0.100D-99|
         0.100+124|
        ********|
        *******|
         -0.1235D-03  0.6022D+24|
           0.60221407599999999E+24|
          0.3142E+01|
         0.3142E+001|
        ************|
          6.0221407599999999E+23|
          1.5000E+00 -1.2346E-04|
          15.000E-01|
          0.0015E+03|
          0.1000E+02|
                        -0.0|
          -0.0001234567890|
           3.142|
          -0.000|
          0.1|
          0.2|
          0.3|
          0.8|
          0.0|
        ****|
            2.|
            4.|
            0.|
            0.|
         10.00|
          0.00|
        0.50|
        .50|
        .00|
        -0.50|
        -.50|
        123456789.12|
                42|
        0042|
         -007|
        **|
        -7|
        *|
             |
         -2147483648|
                 42        -7|
            1   22  333|
            1   22|
          333|
          334   23|
            2|
         42 xyz uvw|
         42 xyz uvw  -7|
         xyz uvw  -7|
        N= 42 it's|
        one|
        two|
        one|
        |
        three|
        only|
        headtail|
        pad|
           a  b|
        T   F|
         F T|
        |
        alone|
        no newline<|
        var fmt42|
        spaces  42|
        lower  42|
        xyz|
        uvw|
        keyword 42|
        """;

    public const string ExpectedReport =
        """
        write    0|
        read after write   -1|
        long    0[first li]|
        short    0[two     ]|
        empty    0[        ]|
        last    0[four    ]|
        eof   -1[untouchd]|
        eof again 5008|
        write at eof    0|
        a2 a    0[fi ][rst]|
        a5 past end    0[two][   ]|
        x and slash    0[rst][two]|
        reversion    0[fir][two]|
        after skip    0[four    ]|
        write in middle    0|
        read after it   -1|
        lines after cut    2|
        lf    0[ab      ]|
        crlf    0[cd      ]|
        no ending    0[noend   ]|
        then   -1|
        formatted write on direct 5001|
        formatted read on direct 5001|
        formatted on unformatted 5001|
        read empty file   -1|
        I with character 5006|
        F with integer 5006|
        I with double 5006|
        bad format 5006|
        unknown descriptor 5006|
        integers    12     0|
        implied point    0    123.4500|
        own point    0      1.5000|
        exponent    0    250.0000|
        d and sign    0     -0.0150    300.0000|
        logical    0 T|
        bad integer 5010|
        f reads whole    0   12.00|
        """;
}
