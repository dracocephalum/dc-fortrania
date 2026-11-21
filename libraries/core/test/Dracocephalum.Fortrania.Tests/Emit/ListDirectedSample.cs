namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that write and read list-directed text. What the tests
/// expect of them is what the reference compiler's program wrote for the
/// same text, line for line; each expected line is followed here by a bar,
/// so that the blanks a line ends in can be seen.
/// </summary>
internal static class ListDirectedSample
{
    public const string Source =
        """
              SUBROUTINE LDOUT ( NAME )
              CHARACTER*(*) NAME
              INTEGER I, K ( 12 ), U
              DOUBLE PRECISION D ( 5 ), E
              REAL R
              LOGICAL L
              CHARACTER*5 C
              CHARACTER*90 LONG
              U = 21
              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW' )
              DO I = 1, 12
                 K ( I ) = I * 1000
              END DO
              D ( 1 ) = 1.5D0
              D ( 2 ) = -0.75D0
              D ( 3 ) = 6.02214076D23
              D ( 4 ) = 0.0D0
              D ( 5 ) = 13.0D0
              R = 3.14159
              L = .TRUE.
              C = 'AB'
              LONG = 'L'
              LONG ( 90:90 ) = 'E'
              WRITE ( U, * ) 42
              WRITE ( U, * ) -7, 0
              WRITE ( U, * ) D ( 1 )
              WRITE ( U, * ) D
              WRITE ( U, * ) R, -R, 0.0, 1.0E10, 1.0E-5, 0.1, 100.0
              WRITE ( U, * ) L, .FALSE.
              WRITE ( U, * ) C
              WRITE ( U, * ) C, C
              WRITE ( U, * ) C, 42, C
              WRITE ( U, * ) 42, C, 1.5D0, C, L, C
              WRITE ( U, * ) 'it''s', ' "q"'
              WRITE ( U, * )
              WRITE ( U, * ) K
              WRITE ( U, * ) ( K ( I ), I = 1, 7 )
              WRITE ( U, * ) LONG
              WRITE ( U, * ) 1, LONG
              WRITE ( U, * ) LONG ( 1:70 ), 12345
              WRITE ( U, * ) LONG ( 1:66 ), 12345
              WRITE ( U, * ) LONG ( 1:67 ), 12345
              WRITE ( U, * ) LONG ( 1:68 ), 12345
              WRITE ( U, * ) 1.0D-1, 0.099999D0, 1.0D16, 1.0D17, 9.99D16
              WRITE ( U, * ) 123456789.0D0, 1.0D-10, 1.0D100, -1.0D-300
              WRITE ( U, * ) 0.1D0 + 0.2D0, 9.9999999999999999D0
              WRITE ( U, * ) 99999999999999999.0D0, 0.99999999999999999D0
              WRITE ( U, * ) 2147483647, -2147483647 - 1
              WRITE ( U, * ) '', 5
              WRITE ( U, * ) 1.0E0, 1.0D0, 1
              WRITE ( U, * ) 123456.7, 1234567.8, 99999999.0, 0.09999, 1.0E-1
              E = 1.0D0
              DO I = 1, 3
                 WRITE ( U, * ) I, E / 3.0D0
              END DO
              CLOSE ( U )
              END

              SUBROUTINE LDIN ( NAME, LOG )
              CHARACTER*(*) NAME, LOG
              INTEGER I, J, K ( 4 ), U, P, IOS
              DOUBLE PRECISION D, E ( 3 )
              REAL R
              LOGICAL L, M, LL ( 4 )
              CHARACTER*6 C, B
              CHARACTER*3 S ( 2 )
              U = 21
              P = 22
              OPEN ( UNIT=P, FILE=LOG, STATUS='NEW' )
              OPEN ( UNIT=U, FILE=NAME, STATUS='NEW' )
              WRITE ( U, '(A)' ) '  12  -3'
              WRITE ( U, '(A)' ) '7,8 , 9,10'
              WRITE ( U, '(A)' ) '1.5 -2.5D3 3E2'
              WRITE ( U, '(A)' ) '42'
              WRITE ( U, '(A)' ) '''quoted text'' "dq" plain'
              WRITE ( U, '(A)' ) '''it''''s'' ''''  ''a'
              WRITE ( U, '(A)' ) 'b'''
              WRITE ( U, '(A)' ) 'T .FALSE. .t. F'
              WRITE ( U, '(A)' ) '1'
              WRITE ( U, '(A)' ) ' '
              WRITE ( U, '(A)' ) '2 3'
              WRITE ( U, '(A)' ) '3*5, 6'
              WRITE ( U, '(A)' ) '1,,3'
              WRITE ( U, '(A)' ) '1 / 2 3'
              WRITE ( U, '(A)' ) '4 5 6 7 extra'
              WRITE ( U, '(A)' ) 'x 2'
              WRITE ( U, '(A)' ) '1.5 2'
              WRITE ( U, '(A)' ) '''abcdefghij'' ''xy'''
              WRITE ( U, '(A)' ) '1 2'
              WRITE ( U, '(A)' ) '5'//CHAR(9)//'6'
              WRITE ( U, '(A)' ) '2*''ab'' 3'
              WRITE ( U, '(A)' ) '9'
              REWIND ( U )
              READ ( U, *, IOSTAT=IOS ) I, J
              WRITE ( P, '(A,3I6)' ) 'two integers', IOS, I, J
              READ ( U, *, IOSTAT=IOS ) K
              WRITE ( P, '(A,5I6)' ) 'commas', IOS, K
              READ ( U, *, IOSTAT=IOS ) E
              WRITE ( P, '(A,I6,3F12.3)' ) 'reals', IOS, E
              READ ( U, *, IOSTAT=IOS ) D
              WRITE ( P, '(A,I6,F12.3)' ) 'integer as real', IOS, D
              READ ( U, *, IOSTAT=IOS ) C, S ( 1 ), B
              WRITE ( P, '(A,I6,7A)' ) 'strings', IOS, '[', C, '][', S ( 1 ),
             .                         '][', B, ']'
              READ ( U, *, IOSTAT=IOS ) C, S ( 1 ), B
              WRITE ( P, '(A,I6,7A)' ) 'quotes', IOS, '[', C, '][', S ( 1 ),
             .                         '][', B, ']'
              READ ( U, *, IOSTAT=IOS ) LL
              WRITE ( P, '(A,I6,4L2)' ) 'logicals', IOS, LL
              READ ( U, *, IOSTAT=IOS ) I, J, K ( 1 )
              WRITE ( P, '(A,4I6)' ) 'across records', IOS, I, J, K ( 1 )
              READ ( U, *, IOSTAT=IOS ) K
              WRITE ( P, '(A,5I6)' ) 'repeat', IOS, K
              K ( 2 ) = -1
              READ ( U, *, IOSTAT=IOS ) ( K ( I ), I = 1, 3 )
              WRITE ( P, '(A,4I6)' ) 'null value', IOS, ( K ( I ), I = 1, 3 )
              K ( 2 ) = -1
              K ( 3 ) = -1
              READ ( U, *, IOSTAT=IOS ) ( K ( I ), I = 1, 3 )
              WRITE ( P, '(A,4I6)' ) 'slash', IOS, ( K ( I ), I = 1, 3 )
              READ ( U, *, IOSTAT=IOS ) I, J
              WRITE ( P, '(A,3I6)' ) 'rest of record dropped', IOS, I, J
              READ ( U, *, IOSTAT=IOS ) I, J
              WRITE ( P, '(A,I6)' ) 'bad integer', IOS
              READ ( U, *, IOSTAT=IOS ) I, J
              WRITE ( P, '(A,I6)' ) 'real for integer', IOS
              READ ( U, *, IOSTAT=IOS ) S
              WRITE ( P, '(A,I6,5A)' ) 'string array', IOS, '[', S ( 1 ), '][',
             .                         S ( 2 ), ']'
              READ ( U, *, IOSTAT=IOS ) R, D
              WRITE ( P, '(A,I6,2F8.2)' ) 'integers as reals', IOS, R, D
              READ ( U, *, IOSTAT=IOS ) I, J
              WRITE ( P, '(A,3I6)' ) 'tab', IOS, I, J
              READ ( U, *, IOSTAT=IOS ) S, I
              WRITE ( P, '(A,I6,5A,I4)' ) 'repeated string', IOS, '[', S ( 1 ),
             .                         '][', S ( 2 ), ']', I
              READ ( U, *, IOSTAT=IOS ) I, J
              WRITE ( P, '(A,I6)' ) 'end in list', IOS
              READ ( U, *, IOSTAT=IOS ) I
              WRITE ( P, '(A,I6)' ) 'after end', IOS
              CLOSE ( U, STATUS='DELETE' )
              CLOSE ( P )
              END
        """;

    public const string ExpectedOutput =
        """
                  42|
                  -7           0|
           1.5000000000000000     |
           1.5000000000000000      -0.75000000000000000        6.0221407599999999E+023   0.0000000000000000        13.000000000000000     |
           3.14159012      -3.14159012       0.00000000       1.00000000E+10   9.99999975E-06  0.100000001       100.000000    |
         T F|
         AB   |
         AB   AB   |
         AB             42 AB   |
                  42 AB      1.5000000000000000      AB    T AB   |
         it's "q"|
        |
                1000        2000        3000        4000        5000        6000        7000        8000        9000       10000       11000       12000|
                1000        2000        3000        4000        5000        6000        7000|
         L                                                                                        E|
                   1 L                                                                                        E|
         L                                                                            12345|
         L                                                                        12345|
         L                                                                         12345|
         L                                                                          12345|
          0.10000000000000001        9.9999000000000005E-002   10000000000000000.        1.0000000000000000E+017   99900000000000000.     |
           123456789.00000000        1.0000000000000000E-010   1.0000000000000000E+100  -1.0000000000000000E-300|
          0.30000000000000004        10.000000000000000     |
           1.0000000000000000E+017   1.0000000000000000     |
          2147483647 -2147483648|
                    5|
           1.00000000       1.0000000000000000                1|
           123456.703       1234567.75       100000000.       9.99900028E-02  0.100000001    |
                   1  0.33333333333333331     |
                   2  0.33333333333333331     |
                   3  0.33333333333333331     |
        """;

    public const string ExpectedReport =
        """
        two integers     0    12    -3|
        commas     0     7     8     9    10|
        reals     0       1.500   -2500.000     300.000|
        integer as real     0      42.000|
        strings     0[quoted][dq ][plain ]|
        quotes     0[it's  ][   ][ab    ]|
        logicals     0 T F T F|
        across records     0     1     2     3|
        repeat     0     5     5     5     6|
        null value     0     1    -1     3|
        slash     0     1    -1    -1|
        rest of record dropped     0     4     5|
        bad integer  5010|
        real for integer  5010|
        string array     0[abc][xy ]|
        integers as reals     0    1.00    2.00|
        tab     0     5     6|
        repeated string     0[ab ][ab ]   3|
        end in list    -1|
        after end  5008|
        """;
}
