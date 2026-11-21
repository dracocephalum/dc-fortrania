namespace Dracocephalum.Fortrania.Tests.Emit;

/// <summary>
/// Program units whose variables share storage. The results the tests
/// expect of them are those the reference compiler gives for the same
/// text, and none depends on the order of bytes in a number.
/// </summary>
internal static class EquivalenceSample
{
    public const string Source =
        """
              SUBROUTINE ADD1 ( N )
              INTEGER N
              N = N + 1
              END

              SUBROUTINE PUN ( D, OUT )
              DOUBLE PRECISION D, DEQUIV
              INTEGER OUT ( * ), IEQUIV ( 2 )
              EQUIVALENCE ( DEQUIV, IEQUIV )
              DEQUIV = D
              OUT ( 1 ) = IEQUIV ( 1 ) + IEQUIV ( 2 )
              IEQUIV ( 1 ) = 0
              IEQUIV ( 2 ) = 0
              OUT ( 2 ) = 0
              IF ( DEQUIV .EQ. 0.0D0 ) OUT ( 2 ) = 1
              END

              INTEGER FUNCTION SHARED ( N )
              INTEGER N, A ( 4 ), B ( 3 ), C
              EQUIVALENCE ( A ( 2 ), B ( 1 ) ), ( B ( 3 ), C )
              SAVE A
              DATA A / 1, 2, 3, 4 /
              C = C + N
              CALL ADD1 ( C )
              B ( 1 ) = B ( 1 ) + 10
              SHARED = A ( 1 ) + 100 * A ( 2 ) + 10000 * A ( 3 )
             .       + 1000000 * A ( 4 )
              END

              SUBROUTINE CHARS ( OUT )
              CHARACTER*8 WHOLE
              CHARACTER*2 PARTS ( 4 )
              CHARACTER*3 MID
              INTEGER OUT ( * )
              EQUIVALENCE ( WHOLE, PARTS ), ( WHOLE ( 4:4 ), MID )
              WHOLE = 'ABCDEFGH'
              OUT ( 1 ) = ICHAR ( PARTS ( 3 ) ( 1:1 ) )
              PARTS ( 2 ) = 'xy'
              OUT ( 2 ) = ICHAR ( WHOLE ( 3:3 ) )
              OUT ( 3 ) = ICHAR ( MID ( 1:1 ) )
              OUT ( 4 ) = ICHAR ( MID ( 3:3 ) )
              END
        """;
}
