// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public class EmptyStream : VirtualAbstractStream, IVirtualMemoryRangeOwner
{
    private readonly Patches.Patch _streamStartPatch;
    private BufferBackedRange.Builder _bufBuilder;
    private BufferBackedRange _buf;

    public EmptyStream(VirtualMemorySystem virtualMemory, KnownStream id) : base(virtualMemory, id)
    {
        _bufBuilder = new BufferBackedRange.Builder(virtualMemory, 16); // just the header, for now
        int offset = 0;
        var firstBlockPatchPoints = InitDataBlock(ref offset, out _streamStartPatch);
        InitDataBlockContent(ref offset, firstBlockPatchPoints);

        virtualMemory.Reservations.Add()
        .OnGetRequetedSize(_bufBuilder.GetRequestedSize)
        .OnSetStartAddress(_bufBuilder.SetStart)
        .OnReservationsComplete(() =>
        {
            _buf = _bufBuilder.Create();
            virtualMemory.AddRange(this);
        });
    }

    private DataBlockPatchPoints InitDataBlock(ref int offset, out Patches.Patch dataBlockStart)
    {
        _bufBuilder.EnsureCapacity(offset, 4 * VirtualMemory.PointerSize);
        dataBlockStart = Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset);
        _bufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
        var ppBegin = _bufBuilder.AddPatchPoint(offset);
        offset += VirtualMemory.PointerSize;
        _bufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
        var ppPos = _bufBuilder.AddPatchPoint(offset);
        offset += VirtualMemory.PointerSize;
        _bufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
        var ppEnd = _bufBuilder.AddPatchPoint(offset);
        offset += VirtualMemory.PointerSize;
        _bufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
        var ppPrev = _bufBuilder.AddPatchPoint(offset);
        offset += VirtualMemory.PointerSize;
        return new DataBlockPatchPoints
        {
            Begin = ppBegin,
            Pos = ppPos,
            End = ppEnd,
            Prev = ppPrev
        };
    }

    private void InitDataBlockContent(ref int offset, DataBlockPatchPoints patchPoints)
    {
        _bufBuilder.EnsureCapacity(offset, 4); // pretend like there's uint32_t worth of empty space in the buffer
        patchPoints.Begin.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
        _bufBuilder.WriteUInt32(offset, 0);
        offset += 4;
        // make pos and end point to the same place, indicating zero entities in the data block
        patchPoints.Pos.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
        patchPoints.End.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
        patchPoints.Prev.SetPatch(Patches.MakeConstPatch(VirtualMemory, VirtualMemory.NullPointer));
    }

    public override ulong Start => _buf.Start;
    public override ulong Count => _buf.Count;

    public override Patches.Patch StreamStartPatch => _streamStartPatch;
    public override Patches.Patch BlockDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0u));

    public override Patches.Patch MaxDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0u));

    public override bool TryReadExtent(ulong start, ulong count, Span<byte> buffer) => _buf.TryReadExtent(start, count, buffer);


    readonly struct DataBlockPatchPoints
    {
        public Patches.PatchPoint Begin { get; init; }
        public Patches.PatchPoint Pos { get; init; }
        public Patches.PatchPoint End { get; init; }
        public Patches.PatchPoint Prev { get; init; }
    }

}

