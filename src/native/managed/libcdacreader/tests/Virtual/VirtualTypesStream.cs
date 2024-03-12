// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public class VirtualTypeStream : VirtualAbstractStream
{
    private ulong _startAddr;
    private ulong _count;
    private VirtualMemorySystem.Reservation _reservation;
    private BufferBackedRange.Builder _bufBuilder;

    private Patches.Patch _dataBlockStartPatch; // where this stream starts
    private BufferBackedRange _buf;
    private DataBlockPatchPoints _firstDataBlockPatchPoints;

    private readonly IReadOnlyCollection<TypeEntity> _entities;

    readonly struct DataBlockPatchPoints
    {
        public Patches.PatchPoint Begin { get; init; }
        public Patches.PatchPoint Pos { get; init; }
        public Patches.PatchPoint End { get; init; }
        public Patches.PatchPoint Prev { get; init; }
    }

    public readonly struct TypeEntity
    {
        public ushort Id { get; init; }
        public ushort Version { get; init; }
        public string Name { get; init; }
        // TODO: field offsets
    }
    public VirtualTypeStream(VirtualMemorySystem virtualMemory, TypeEntity[] entities = null) : base(virtualMemory, (ushort)KnownStreams.Types)
    {
        if (entities == null)
            entities = Array.Empty<TypeEntity>();
        if (entities.Length > 0)
        {
            throw new NotImplementedException("TODO: implement");
        }
        _entities = entities;

        _reservation = virtualMemory.Reservations.Add();
        _reservation.OnGetRequetedSize(GetRequestedSize);
        _reservation.OnSetStartAddress(SetStart);
        _reservation.OnReservationsComplete(ReservationsComplete);
        int offset = 0;

        // most recent struct data_block__ of the stream
        // TODO: multiple blocks
        int dataBlockHeaderSize = 4 * virtualMemory.PointerSize; // begin, pos, end, prev
        _bufBuilder = BufferBackedRange.Build(virtualMemory, dataBlockHeaderSize);
        _firstDataBlockPatchPoints = InitDataBlock(ref offset, out _dataBlockStartPatch);

        _buf = null;
        _count = (ulong)offset;
    }

    public override ulong Start => _startAddr;
    public override ulong Count => _count;

    // FIXME: this is wrong, but the reader doesn't actually look at these values
    public override Patches.Patch BlockDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0u));
    public override Patches.Patch MaxDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0u));

    public override Patches.Patch StreamStartPatch => _dataBlockStartPatch;

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
        return new DataBlockPatchPoints
        {
            Begin = ppBegin,
            Pos = ppPos,
            End = ppEnd,
            Prev = ppPrev
        };
    }

    private ulong GetRequestedSize() => _count;
    private void SetStart(VirtualMemorySystem.ExternalPtr startAddr)
    {
        _startAddr = VirtualMemory.ToInternalPtr(startAddr);
        _bufBuilder.SetStart(startAddr);
    }

    public VirtualMemorySystem.Reservation Reservation => _reservation;

    private void ReservationsComplete()
    {
        _reservation = null;

        _buf = _bufBuilder.Create();
        _bufBuilder = null;
    }

    public override bool TryReadExtent(ulong start, ulong count, Span<byte> buffer) => _buf.TryReadExtent(start, count, buffer);
}
