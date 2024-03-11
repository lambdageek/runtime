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
public class VirtualDataContext : IVirtualMemoryRangeOwner
{
    // Create a well-formed context at the given base address
    public static VirtualDataContext CreateGoodContext(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalPtr baseAddress, VirtualAbstractStream[] virtualStreams = null)
    {
        if (virtualStreams != null)
            throw new NotImplementedException("Virtual streams are not yet supported");
        return new VirtualDataContext(virtualMemory, baseAddress);
    }

    private VirtualDataContext(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalPtr start)
    {
        _virtualMemory = virtualMemory;
        var headerSize = (ushort)(CorrectDataContractHeaderSize + virtualMemory.PointerSize); /* header and pointer to start of streams */
        var builder = BufferBackedRange.Build(virtualMemory, (ulong)headerSize);
        builder.SetStart(start);
        _malformed = false;
        int streamCount = 0;
        FillGoodHeader(builder, headerSize, streamCount);
        _buf = builder.Create();
    }

    private void FillGoodHeader(BufferBackedRange.Builder builder, ushort headerSize, int streamCount)
    {
        builder.WriteUInt32(0, Magic);
        builder.WriteUInt16(4, headerSize);
        builder.WriteUInt16(6, (ushort)1); //version
        builder.WriteUInt32(8, 0u); // reserved
        builder.WriteUInt32(12, (uint)streamCount);
        builder.WriteExternalPtr(16, _virtualMemory.NullPointer);
    }

    public const int CorrectDataContractHeaderSize = 16; // magic(4) + size(2) + version(2) + reserved(4) + streamCount(4)
    public const uint Magic = 0x646e6300u; // "dnc\0"

    private readonly VirtualMemorySystem _virtualMemory;
    public ulong Start => _buf.Start;
    public ulong Count => _buf.Count;
    private readonly BufferBackedRange _buf;

    private readonly bool _malformed;

    public virtual bool TryReadExtent(ulong start, ulong count, Span<byte> buffer)
    {
        if (_malformed)
            throw new NotImplementedException("Malformed context isn't implemented yet");
        return _buf.TryReadExtent(start, count, buffer);
    }

}
