// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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
    public static void CreateGoodContext(VirtualMemorySystem virtualMemory, IReadOnlyCollection<VirtualAbstractStream>? virtualStreams, Action<VirtualMemorySystem.ExternalPtr> OnCreate)
    {
        if (virtualStreams == null)
            virtualStreams = Array.Empty<VirtualAbstractStream>();
        VirtualMemorySystem.Reservation res = virtualMemory.Reservations.Add();
        var builder = new VirtualDataContextBuilder(virtualMemory, virtualStreams);
        res.OnGetRequetedSize(builder.GetRequestedSize)
        .OnSetStartAddress(builder.SetStart)
        .OnReservationsComplete(() =>
        {
            var dataContext = builder.Create();
            virtualMemory.AddRange(dataContext);
            OnCreate(virtualMemory.ToExternalPtr(dataContext.Start));
        });
    }

    private class VirtualDataContextBuilder
    {
        private readonly BufferBackedRange.Builder _bufBuilder;
        private readonly VirtualMemorySystem _virtualMemory;

        public VirtualDataContextBuilder(VirtualMemorySystem virtualMemory, IReadOnlyCollection<VirtualAbstractStream> virtualStreams)
        {
            _virtualMemory = virtualMemory;
            int streamCount = virtualStreams.Count;
            var headerSize = (ushort)(CorrectDataContractHeaderSize + virtualMemory.PointerSize); /* header and pointer to start of streams */
            int streamHeaderSize = streamCount * (4 * virtualMemory.PointerSize); // FIXME: assumes sizeof(size_t) == sizeof(void*)
            _bufBuilder = BufferBackedRange.Build(virtualMemory, (ulong)((int)headerSize + streamHeaderSize));
            int offset = 0;
            BufferBackedRange.Builder.Patch dataContextStartPatch = _bufBuilder.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset);
            FillGoodHeader(headerSize, streamCount, ref offset, out BufferBackedRange.Builder.PatchPoint streamHeaderPtr);
            int streamHeaderStartOffset = offset;
            streamHeaderPtr.SetPatch(_bufBuilder.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, streamHeaderStartOffset));
            AddStreamHeader(virtualStreams, dataContextStartPatch, ref offset);
            if (offset != (int)headerSize + streamHeaderSize)
                throw new InvalidOperationException("Final offset didn't match expected size");
        }

        public ulong GetRequestedSize() => _bufBuilder.GetRequestedSize();

        public void SetStart(VirtualMemorySystem.ExternalPtr start) => _bufBuilder.SetStart(start);

        public VirtualDataContext Create()
        {
            var buf = _bufBuilder.Create();
            return new VirtualDataContext(_virtualMemory, buf, malformed: false);
        }
        private void FillGoodHeader(ushort headerSize, int streamCount, ref int offset, out BufferBackedRange.Builder.PatchPoint streamHeaderPtr)
        {
            int off = offset;
            _bufBuilder.WriteUInt32(off + 0, Magic);
            _bufBuilder.WriteUInt16(off + 4, headerSize);
            _bufBuilder.WriteUInt16(off + 6, (ushort)1); //version
            _bufBuilder.WriteUInt32(off + 8, 0u); // reserved
            _bufBuilder.WriteUInt32(off + 12, (uint)streamCount);
            _bufBuilder.WriteExternalPtr(off + 16, _virtualMemory.NullPointer);
            streamHeaderPtr = _bufBuilder.AddPatchPoint(off + 16);
            offset += 16 + _virtualMemory.PointerSize;
        }

        public void AddStreamHeader(IReadOnlyCollection<VirtualAbstractStream> virtualStreams, BufferBackedRange.Builder.Patch dataContextStartPatch, ref int offset)
        {
            int off = offset;
            int streamNum = 0;
            foreach (var stream in virtualStreams)
            {
                if (streamNum != (int)stream.Id)
                {
                    throw new InvalidOperationException("Stream IDs must be contiguous and start at 0");
                }
                // struct data_stream__
                _bufBuilder.WriteExternalPtr(off, _virtualMemory.ToExternalPtr(stream.Start)); // TODO: patchpoint
                off += _virtualMemory.PointerSize;
                _bufBuilder.WriteExternalSizeT(off, stream.BlockDataSize);
                off += _virtualMemory.PointerSize;
                _bufBuilder.WriteExternalSizeT(off, stream.MaxDataSize);
                off += _virtualMemory.PointerSize;
                _bufBuilder.WriteExternalPtr(off, _virtualMemory.NullPointer);
                _bufBuilder.AddPatchPoint(off).SetPatch(dataContextStartPatch);
                off += _virtualMemory.PointerSize;
                streamNum++;
            }
            offset = off;
        }


    }

    public const int CorrectDataContractHeaderSize = 16; // magic(4) + size(2) + version(2) + reserved(4) + streamCount(4)
    public const uint Magic = 0x646e6300u; // "dnc\0"

    private readonly VirtualMemorySystem _virtualMemory;
    private readonly BufferBackedRange _buf;
    public ulong Start => _buf.Start;
    public ulong Count => _buf.Count;

    private readonly bool _malformed;

    private VirtualDataContext(VirtualMemorySystem virtualMemory, BufferBackedRange buf, bool malformed)
    {
        _virtualMemory = virtualMemory;
        _buf = buf;
        _malformed = malformed;
    }

    public virtual bool TryReadExtent(ulong start, ulong count, Span<byte> buffer)
    {
        if (_malformed)
            throw new NotImplementedException("Malformed context isn't implemented yet");
        return _buf.TryReadExtent(start, count, buffer);
    }

}
