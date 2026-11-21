namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// A program unit that reads and writes character variables as files,
/// backspaces a file and shifts bits. What the tests expect of it is what
/// the reference compiler's program wrote for the same text, line for line;
/// each expected line is followed here by a bar.
/// </summary>
internal static class InternalFileSample
{
    public const string Source =
        """
              SUBROUTINE INTF ( NAME, LOG )
              CHARACTER*(*) NAME, LOG
              CHARACTER*8 S, T ( 3 )
              INTEGER IOS, I, J, K, L, P
              DOUBLE PRECISION D
              P = 22
              OPEN ( P, FILE=LOG, STATUS='NEW' )
              S = 'xxxxxxxx'
              WRITE ( S, '(I4)' ) 42
              WRITE ( P, '(3A)' ) '[', S, ']'
              WRITE ( S, '(I4.4)', IOSTAT=IOS ) 7
              WRITE ( P, '(3A,I6)' ) '[', S, ']', IOS
              WRITE ( S, '(A)', IOSTAT=IOS ) 'much too long'
              WRITE ( P, '(3A,I6)' ) '[', S, ']', IOS
              WRITE ( S, '(I4,/,I4)', IOSTAT=IOS ) 1, 2
              WRITE ( P, '(A,I6)' ) 'past one record', IOS
              T ( 1 ) = 'zzzzzzzz'
              T ( 2 ) = 'zzzzzzzz'
              T ( 3 ) = 'zzzzzzzz'
              WRITE ( T, '(I3,/,I3)', IOSTAT=IOS ) 1, 2
              WRITE ( P, '(7A,I6)' ) '[', T(1), '][', T(2), '][', T(3), ']', IOS
              WRITE ( T, '(I3)', IOSTAT=IOS ) 4, 5, 6, 7
              WRITE ( P, '(A,I6)' ) 'past three records', IOS
              S = 'xxxxxxxx'
              WRITE ( S ( 3:6 ), '(A)' ) 'ab'
              WRITE ( P, '(3A)' ) '[', S, ']'
              WRITE ( T ( 2 ), '(F8.2)' ) 3.14159D0
              WRITE ( P, '(3A)' ) '[', T(2), ']'
              WRITE ( S, *, IOSTAT=IOS ) 12
              WRITE ( P, '(A,I6)' ) 'list too long', IOS
              S = '  12 -34'
              READ ( S, '(2I4)', IOSTAT=IOS ) I, J
              WRITE ( P, '(A,3I6)' ) 'read', IOS, I, J
              READ ( S, *, IOSTAT=IOS ) I, J
              WRITE ( P, '(A,3I6)' ) 'list', IOS, I, J
              T ( 1 ) = '1'
              T ( 2 ) = '2'
              T ( 3 ) = '3'
              READ ( T, '(I8)', IOSTAT=IOS ) I, J, K
              WRITE ( P, '(A,4I6)' ) 'records', IOS, I, J, K
              READ ( T, '(I8)', IOSTAT=IOS ) I, J, K, L
              WRITE ( P, '(A,I6)' ) 'past end', IOS
              READ ( T ( 2 ), '(I8)', IOSTAT=IOS ) I
              WRITE ( P, '(A,2I6)' ) 'element', IOS, I
              READ ( S, '(I2)', IOSTAT=IOS ) I
              WRITE ( P, '(A,2I6)' ) 'short', IOS, I
              S = '1.5D2'
              READ ( S, *, IOSTAT=IOS ) D
              WRITE ( P, '(A,I6,F8.1)' ) 'list real', IOS, D

              OPEN ( 11, FILE=NAME, STATUS='NEW' )
              WRITE ( 11, '(A)' ) 'one'
              WRITE ( 11, '(A)' ) 'two'
              WRITE ( 11, '(A)' ) 'three'
              BACKSPACE ( 11, IOSTAT=IOS )
              WRITE ( P, '(A,I6)' ) 'backspace after write', IOS
              READ ( 11, '(A)', IOSTAT=IOS ) S
              WRITE ( P, '(A,I6,3A)' ) 'reads', IOS, '[', S, ']'
              REWIND ( 11 )
              READ ( 11, '(A)' ) S
              READ ( 11, '(A)' ) S
              BACKSPACE ( 11 )
              READ ( 11, '(A)' ) S
              WRITE ( P, '(3A)' ) 'again [', S, ']'
              BACKSPACE ( 11 )
              BACKSPACE ( 11 )
              READ ( 11, '(A)' ) S
              WRITE ( P, '(3A)' ) 'twice [', S, ']'
              BACKSPACE ( 11 )
              BACKSPACE ( 11 )
              BACKSPACE ( 11, IOSTAT=IOS )
              WRITE ( P, '(A,I6)' ) 'at start', IOS
              READ ( 11, '(A)' ) S
              WRITE ( P, '(3A)' ) 'first [', S, ']'
              READ ( 11, '(A)' ) S
              READ ( 11, '(A)' ) S
              READ ( 11, '(A)', IOSTAT=IOS ) S
              WRITE ( P, '(A,I6)' ) 'eof', IOS
              BACKSPACE ( 11, IOSTAT=IOS )
              WRITE ( P, '(A,I6)' ) 'backspace at eof', IOS
              READ ( 11, '(A)', IOSTAT=IOS ) S
              WRITE ( P, '(A,I6,3A)' ) 'after', IOS, '[', S, ']'
              BACKSPACE ( 11 )
              BACKSPACE ( 11 )
              READ ( 11, '(A)', IOSTAT=IOS ) S
              WRITE ( P, '(A,I6,3A)' ) 'then', IOS, '[', S, ']'
              CLOSE ( 11, STATUS='DELETE' )
              BACKSPACE ( 19, IOSTAT=IOS )
              WRITE ( P, '(A,I6)' ) 'unconnected', IOS
              OPEN ( 12, FILE=NAME, STATUS='NEW', ACCESS='DIRECT', RECL=4 )
              BACKSPACE ( 12, IOSTAT=IOS )
              WRITE ( P, '(A,I6)' ) 'direct', IOS
              CLOSE ( 12, STATUS='DELETE' )
              I = 33
              J = -33
              WRITE ( P, '(A,10I12)' ) 'ishft', ISHFT ( 1, 4 ),
             .   ISHFT ( 256, -4 ), ISHFT ( -1, -28 ), ISHFT ( -16, -2 ),
             .   ISHFT ( 1, 31 ), ISHFT ( 5, 0 ), ISHFT ( 3, 30 ),
             .   ISHFT ( 1, -1 ), ISHFT ( 1, I ), ISHFT ( -1, J )
              WRITE ( P, '(A,2I12)' ) 'ishft more',
             .   ISHFT ( 123456789, 24 ), ISHFT ( -123456789, -24 )
              CLOSE ( P )
              END
        """;

    public const string ExpectedReport =
        """
        [  42    ]|
        [0007    ]     0|
        [0007    ]    -2|
        past one record    -1|
        [  1     ][  2     ][zzzzzzzz]     0|
        past three records    -1|
        [xxab  xx]|
        [    3.14]|
        list too long    -2|
        read     0    12   -34|
        list     0    12   -34|
        records     0     1     2     3|
        past end    -1|
        element     0     2|
        short     0     0|
        list real     0   150.0|
        backspace after write     0|
        reads     0[three   ]|
        again [two     ]|
        twice [one     ]|
        at start     0|
        first [one     ]|
        eof    -1|
        backspace at eof     0|
        after    -1[three   ]|
        then     0[three   ]|
        unconnected  5005|
        direct  5001|
        ishft          16          16          15  1073741820 -2147483648           5 -1073741824           0           0           0|
        ishft more   352321536         248|
        """;
}
