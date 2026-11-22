using System.Runtime.InteropServices;

namespace Dracocephalum.Fortrania.Benchmarks;

/// <summary>
/// Stands in for what a context would hold if where a method keeps its
/// working buffers were a setting of the context: the setting, and the
/// block the buffers lie in when it is not the stack. The runtime's own
/// context has neither yet; this is the shape measured before it gets them.
/// </summary>
internal sealed unsafe class FrameChoice
{
    private const int FramesSize = 64 * 1024;

    public FrameChoice(bool localsOnStack)
    {
        LocalsOnStack = localsOnStack;
        Frames = (byte*)NativeMemory.AllocZeroed(FramesSize);
    }

    /// <summary>Gets a value indicating whether a method takes its buffers from the stack.</summary>
    public bool LocalsOnStack { get; }

    /// <summary>Gets the block a method's buffers lie in otherwise, each method's at a place of its own.</summary>
    public byte* Frames { get; private set; }

    /// <summary>Gives the block back. Not a disposal: the library that runs a benchmark calls a method it is shown, and knows nothing of disposing.</summary>
    public void Release()
    {
        NativeMemory.Free(Frames);
        Frames = null;
    }
}
