// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;


// Design:
//  Want to have a VirtualMemorySystem that can direct reads to different ranges.
//  The VirtualDataContext will be the main entry point, and will have a list of VirtualStreams.
//
//  We will want to have a little allocator in the VirtualMemorySystem so we can allocate memory for the growable chunks.
//
// then we can just Create() a VirtualDataContext in a virtual memory system and then read from it.
//
// to test out a data stream reader, we will hand out a Read function from the VirtualMemorySystem
public class VirtualDataContext : IVirtualMemoryRange
{
    // TODO
    public abstract class VirtualStream /*: IVirtualMemoryRange*/
    {

    }
    // Create a well-formed context at the given base address
    public static VirtualDataContext CreateGoodContext(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalPtr baseAddress, VirtualStream[] virtualStreams = null)
    {
        if (virtualStreams != null)
            throw new NotImplementedException("Virtual streams are not yet supported");
        return new VirtualDataContext(virtualMemory, baseAddress);
    }

    private VirtualDataContext(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalPtr start)
    {
        _virtualMemory = virtualMemory;
        Start = virtualMemory.ToInternalPtr(start);
        Count = (ulong)(CorrectDataContractHeaderSize + virtualMemory.PointerSize); /* header and pointer to start of streams */
        _malformed = false;
        int streamCount = 0;
        byte[] buf = new byte[CorrectDataContractHeaderSize + virtualMemory.PointerSize];
        FillGoodHeader(buf, streamCount);
        _buf = buf.AsMemory();
    }

    private void FillGoodHeader(Span<byte> dest, int streamCount)
    {
        _virtualMemory.WriteUInt32(dest, Magic);
        _virtualMemory.WriteUInt16(dest.Slice(4), (ushort)(CorrectDataContractHeaderSize + _virtualMemory.PointerSize));
        _virtualMemory.WriteUInt16(dest.Slice(6), (ushort)1);
        _virtualMemory.WriteUInt32(dest.Slice(8), 0u);
        _virtualMemory.WriteUInt32(dest.Slice(12), (uint)streamCount);
        _virtualMemory.WriteExternalPtr(dest.Slice(16), _virtualMemory.NullPointer);
    }

    public const int CorrectDataContractHeaderSize = 16; // magic(4) + size(2) + version(2) + reserved(4) + streamCount(4)
    public const uint Magic = 0x646e6300u; // "dnc\0"

    private readonly VirtualMemorySystem _virtualMemory;
    public ulong Start { get; }
    public ulong Count { get; }

    private readonly ReadOnlyMemory<byte> _buf;

    private readonly bool _malformed;

    public virtual bool TryReadExtent(ulong start, ulong count, Span<byte> buffer)
    {
        if (_malformed)
            throw new NotImplementedException("Malformed context isn't implemented yet");
        if (start < Start || start + count > Start + Count)
            return false;
        ReadOnlySpan<byte> slice = _buf.Span.Slice((int)(start - Start), (int)count);
        slice.CopyTo(buffer);
        return true;
    }

}
