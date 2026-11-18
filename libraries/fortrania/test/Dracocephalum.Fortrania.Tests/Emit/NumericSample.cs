namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units that between them use every numeric construct the emitter
/// translates. The results the tests expect of them are those the reference
/// compiler gives for the same text.
/// </summary>
internal static class NumericSample
{
    public const string Source =
        """
              DOUBLE PRECISION FUNCTION ARITH ( X, N )
              IMPLICIT NONE
              DOUBLE PRECISION X
              INTEGER N
              DOUBLE PRECISION HALF
              PARAMETER ( HALF = 0.5D0 )
              ARITH = X**2 + X**N + X**(-N) + X**HALF + N/2 + N**3
             .      - MOD ( N, 3 ) + SIGN ( X, -1.0D0 ) + NINT ( X )
             .      + INT ( -X )
             .      + ABS ( -X ) * SQRT ( X ) - ( X - ( X - 1 ) ) + -X * 2
             .      + MAX ( X, 2.0D0, HALF ) + MIN ( N, 9, 4 ) + DBLE ( N ) / 4
             .      + ATAN2 ( X, 2.0D0 ) + EXP ( HALF ) + LOG ( X ) + 2**N
             .      + ANINT ( X ) + AINT ( -X ) + DIM ( X, 1.0D0 )
             .      + MOD ( X, 2.0D0 )
              END

              INTEGER FUNCTION IARITH ( I, J )
              IMPLICIT NONE
              INTEGER I, J
              IARITH = I / J + ( -I ) / J + MOD ( -I, J ) + I ** J + J ** ( -1 )
             .       + ISIGN ( I, -J ) + IABS ( -I ) + NINT ( 2.5 )
             .       + NINT ( -2.5 )
             .       + INT ( 3.99 ) + INT ( -3.99D0 ) + IDIM ( I, J )
             .       + IAND ( I, 12 )
             .       + IOR ( I, 8 ) + IEOR ( I, J ) + MAX0 ( I, J )
             .       - I * J
              END

              REAL FUNCTION RARITH ( R, N )
              IMPLICIT NONE
              REAL R
              INTEGER N
              RARITH = R / 3 + R ** N + R ** 0.5 + SQRT ( R ) + AMAX0 ( N, 2 )
             .       + MAX1 ( R, 1.5 ) + REAL ( N ) / 7 + FLOAT ( N ) * 0.1
              END

              SUBROUTINE LOOPS ( N, OUT )
              IMPLICIT NONE
              INTEGER N, OUT ( 8 )
              INTEGER I, K
              REAL R
              DOUBLE PRECISION D
              OUT ( 1 ) = 0
              DO I = 1, N
                 OUT ( 1 ) = OUT ( 1 ) + I
              END DO
              OUT ( 2 ) = I
              OUT ( 3 ) = 0
              DO I = N, 1, -2
                 OUT ( 3 ) = OUT ( 3 ) + I
              END DO
              OUT ( 4 ) = I
              DO I = 5, 1
                 OUT ( 3 ) = -999
              END DO
              OUT ( 5 ) = I
              K = 3
              OUT ( 6 ) = 0
              DO I = 1, N, K
                 K = K + 1
                 OUT ( 6 ) = OUT ( 6 ) + I
              END DO
              OUT ( 7 ) = 0
              DO R = 0.0, 1.0, 0.25
                 OUT ( 7 ) = OUT ( 7 ) + 1
              END DO
              OUT ( 8 ) = 0
              DO D = 2.0D0, 0.5D0, -0.5D0
                 OUT ( 8 ) = OUT ( 8 ) + NINT ( D * 10 )
              END DO
              OUT ( 8 ) = OUT ( 8 ) + NINT ( D * 1000 )
              END

              DOUBLE PRECISION FUNCTION ARRAYS ( N, A, B )
              IMPLICIT NONE
              INTEGER N
              DOUBLE PRECISION A ( N, * ), B ( 0:N )
              DOUBLE PRECISION L ( 2, 3 ), M ( -1:1, 2 )
              INTEGER I, J
              DO J = 1, 3
                 DO I = 1, 2
                    L ( I, J ) = 10 * I + J
                 END DO
              END DO
              DO I = -1, 1
                 M ( I, 1 ) = I
                 M ( I, 2 ) = 100 * I
              END DO
              ARRAYS = 0
              DO J = 1, 2
                 DO I = 1, N
                    ARRAYS = ARRAYS + A ( I, J ) * B ( I - 1 )
                 END DO
              END DO
              ARRAYS = ARRAYS + L ( 2, 3 ) + L ( 1, 2 ) * M ( -1, 2 )
             .       + M ( 1, 1 )
             .       + B ( N ) + A ( N, 2 )
              A ( 1, 1 ) = L ( 2, 1 )
              END

              SUBROUTINE SWAP ( A, B )
              IMPLICIT NONE
              DOUBLE PRECISION A, B, T
              T = A
              A = B
              B = T
              END

              SUBROUTINE FILL ( N, V, X )
              IMPLICIT NONE
              INTEGER N, I
              DOUBLE PRECISION V ( N ), X
              DO I = 1, N
                 V ( I ) = X
              END DO
              N = N + 100
              END

              LOGICAL FUNCTION ODD ( N )
              IMPLICIT NONE
              INTEGER N
              ODD = MOD ( N, 2 ) .NE. 0
              END

              SUBROUTINE FLIP ( L )
              IMPLICIT NONE
              LOGICAL L
              L = .NOT. L
              END

              DOUBLE PRECISION FUNCTION CALLS ( X, Y )
              IMPLICIT NONE
              DOUBLE PRECISION X, Y, V ( 6 ), ARITH
              INTEGER K, IARITH
              LOGICAL ODD, YES, NO
              EXTERNAL ARITH
              CALL SWAP ( X, Y )
              K = 6
              CALL FILL ( K, V, X + 1 )
              CALL FILL ( K - 104, V ( 3 ), 7.0D0 )
              CALL SWAP ( V ( 1 ), V ( 6 ) )
              CALL SWAP ( V ( 2 ), Y )
              YES = ODD ( 3 )
              NO = ODD ( IARITH ( 7, 2 ) + 1 )
              CALL FLIP ( NO )
              CALL FLIP ( NO )
              CALLS = V ( 1 ) + 10 * V ( 2 ) + 100 * V ( 3 )
             .      + 1000 * V ( 5 ) + K
             .      + ARITH ( 2.0D0, IARITH ( 7, 2 ) / 100 + 1 )
              IF ( YES .AND. .NOT. NO ) CALLS = CALLS + 0.25D0
              IF ( ODD ( K ) .OR. ODD ( 4 ) ) CALLS = -1
              END

              INTEGER FUNCTION BRANCH ( N, FLAG )
              IMPLICIT NONE
              INTEGER N, K
              LOGICAL FLAG, BOTH
              BOTH = FLAG .EQV. ( N .GT. 0 )
              IF ( N .LT. 0 ) THEN
                 BRANCH = -1
                 RETURN
              ELSE IF ( N .EQ. 0 .OR. ( FLAG .NEQV. BOTH ) ) THEN
                 BRANCH = 0
              ELSE
                 BRANCH = 1
                 K = N
                 DO WHILE ( K .GT. 1 )
                    IF ( MOD ( K, 2 ) .EQ. 0 ) THEN
                       K = K / 2
                    ELSE
                       K = 3 * K + 1
                    END IF
                    BRANCH = BRANCH + 1
                 END DO
              END IF
              IF ( BOTH ) BRANCH = BRANCH + 1000
              END
        """;
}
