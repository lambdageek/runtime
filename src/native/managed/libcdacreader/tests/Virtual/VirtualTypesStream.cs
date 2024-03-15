// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime.ICorDebug;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public class VirtualTypeStream : VirtualAbstractStream, IVirtualMemoryRangeOwner
{
    private ulong _startAddr;
    private ulong _count;
    private VirtualMemorySystem.Reservation _reservation;
    private BufferBackedRange.Builder _bufBuilder;

    private Patches.Patch _dataBlockStartPatch; // where this stream starts
    private BufferBackedRange _buf;
    private DataBlockPatchPoints _firstDataBlockPatchPoints;

    private readonly IReadOnlyCollection<TypeEntity> _entities;

    private readonly TypeEntityWriter _typeEntityWriter;
    readonly struct DataBlockPatchPoints
    {
        public Patches.PatchPoint Begin { get; init; }
        public Patches.PatchPoint Pos { get; init; }
        public Patches.PatchPoint End { get; init; }
        public Patches.PatchPoint Prev { get; init; }
    }

    public readonly struct TypeEntity
    {
        public TypeDetails Details { get; init; }
        public uint TotalSize { get; init; }
        public FieldOffset[] FieldOffsets { get; init; }
    }

    public readonly struct TypeDetails
    {
        public ushort Id { get; init; }
        public ushort Version { get; init; }
        public string Name { get; init; }
        // TODO: field offsets
    }

    public readonly struct FieldOffset
    {
        public ushort TypeId { get; init; }
        public ushort Offset { get; init; }
    }

    public VirtualTypeStream(VirtualMemorySystem virtualMemory, TypeEntity[] entities) : base(virtualMemory, KnownStream.Types)
    {
        if (entities == null || entities.Length == 0)
        {
            throw new InvalidOperationException("Must have at least one type entity");
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

        _typeEntityWriter = new TypeEntityWriter(VirtualMemory, _bufBuilder);

        Patches.PatchPoint[] typeDetailsPatchPoints = new Patches.PatchPoint[entities.Length];
        InitDataBlockContent(ref offset, entities, _firstDataBlockPatchPoints, typeDetailsPatchPoints);

        Patches.PatchPoint[] namesPatchPoints = new Patches.PatchPoint[entities.Length];
        InitTypeDetails(ref offset, entities, typeDetailsPatchPoints, namesPatchPoints);
        InitTypeNames(ref offset, entities, namesPatchPoints);
        _count = (ulong)offset;
        if (_bufBuilder.GetRequestedSize() != _count)
        {
            throw new InvalidOperationException("BufferBuilder.GetRequestedSize() ({_bufBuilder.GetRequestedSize()}) != _count ({_count})");
        }
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
        offset += VirtualMemory.PointerSize;
        return new DataBlockPatchPoints
        {
            Begin = ppBegin,
            Pos = ppPos,
            End = ppEnd,
            Prev = ppPrev
        };
    }

    private void InitDataBlockContent(ref int offset, IReadOnlyCollection<TypeEntity> entities, DataBlockPatchPoints patchPoints, Patches.PatchPoint[] typeDetailPatchPoints)
    {
        // FIXME:
        //   Blocks are filled in reverse order (`end` to `begin`) to ensure reading of a stream is always performed in reverse chronological order.
        patchPoints.Begin.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
        patchPoints.Pos.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
        int idx = 0;
        int startOffset = offset;
        foreach (var entity in entities)
        {
            _typeEntityWriter.WriteEntity(entity, ref offset);
            typeDetailPatchPoints[idx++] = _typeEntityWriter.LastTypeDetailsPatchPoint; // yuck
        }
        patchPoints.End.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
        patchPoints.Prev.SetPatch(Patches.MakeConstPatch(VirtualMemory, VirtualMemory.NullPointer));
    }
    private ulong GetRequestedSize() => _count;
    private void SetStart(VirtualMemorySystem.ExternalPtr startAddr)
    {
        _startAddr = VirtualMemory.ToInternalPtr(startAddr);
        _bufBuilder.SetStart(startAddr);
    }

    private void InitTypeDetails(ref int offset, IReadOnlyList<TypeEntity> entities, IReadOnlyList<Patches.PatchPoint> typeDetailPatchPoints, Patches.PatchPoint[] namesPatchPoints)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            var patchPoint = typeDetailPatchPoints[i];
            // patch up the type entity to point to the type details we're about to write
            patchPoint.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
            WriteTypeDetailsPayload(entity.Details, ref offset, out namesPatchPoints[i]);
        }
    }

    private void InitTypeNames(ref int offset, IReadOnlyList<TypeEntity> entities, IReadOnlyList<Patches.PatchPoint> namesPatchPoints)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            WriteTypeName(ref offset, entity.Details.Name, namesPatchPoints[i]);
        }
    }

    public VirtualMemorySystem.Reservation Reservation => _reservation;

    private void ReservationsComplete()
    {
        _reservation = null;

        _buf = _bufBuilder.Create();
        _bufBuilder = null;
        VirtualMemory.AddRange(this);
    }

    public override bool TryReadExtent(ulong start, ulong count, Span<byte> buffer) => _buf.TryReadExtent(start, count, buffer);

    public int GetTypeDetailsSize(TypeDetails details)
    {
        int size = 0;
        size += 2; // uint16_t type;
        size += 2; // uint16_t version;
        size += 2; // uint16_t reserved; // Must be zero
        size += 2; // uint16_t name_len; // Includes nul
        size += VirtualMemory.PointerSize; // Encoding.UTF8.GetByteCount(details.Name) + 1;
        // TODO: field offset details
        return size;
    }

    public void WriteTypeDetailsPayload(TypeDetails entity, ref int offset, out Patches.PatchPoint namePointerPatch)
    {
        _bufBuilder.EnsureCapacity(offset, GetTypeDetailsSize(entity));
        _bufBuilder.WriteUInt16(offset, entity.Id);
        offset += 2;
        _bufBuilder.WriteUInt16(offset, entity.Version);
        offset += 2;
        _bufBuilder.WriteUInt16(offset, 0); // reserved
        offset += 2;
        int byteCount = Encoding.UTF8.GetByteCount(entity.Name) + 1; // include nul
        _bufBuilder.WriteUInt16(offset, (ushort)byteCount); // name_len
        offset += 2;
        _bufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
        namePointerPatch = _bufBuilder.AddPatchPoint(offset);
        offset += VirtualMemory.PointerSize;
    }

    public void WriteTypeName(ref int offset, string name, Patches.PatchPoint namePointerPatch)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        _bufBuilder.EnsureCapacity(offset, bytes.Length + 1);
        namePointerPatch.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(_bufBuilder, offset));
        _bufBuilder.WriteBytes(offset, bytes);
        offset += bytes.Length + 1; // include null terminator
    }

    public static int FieldOffsetSize => 4; // 2*uint16_t

    class TypeEntityWriter : StreamEntityWriter<TypeEntity>
    {
        VirtualMemorySystem _virtualMemory;
        public TypeEntityWriter(VirtualMemorySystem virtualMemory, BufferBackedRange.Builder bufBuilder) : base(bufBuilder)
        {
            _virtualMemory = virtualMemory;
        }

        // hack: WritePayload sets this to the location where the pointer to the type details should go
        public Patches.PatchPoint LastTypeDetailsPatchPoint { get; private set; }

        public override int GetPayloadSize(TypeEntity payload)
        {
            int size = 0;
            size += _virtualMemory.PointerSize; // type_details_t* type_details;
            size += _virtualMemory.PointerSize; // size_t total_size;
            size += FieldOffsetSize * payload.FieldOffsets.Length; // inlined field_offset_t array
            return size;
        }

        public override void WritePayload(TypeEntity payload, ref int offset)
        {
            LastTypeDetailsPatchPoint = BufferBuilder.AddPatchPoint(offset);
            BufferBuilder.WriteExternalPtr(offset, _virtualMemory.NullPointer);
            offset += _virtualMemory.PointerSize;
            BufferBuilder.WriteUInt32(offset, payload.TotalSize);
            offset += _virtualMemory.PointerSize;
            foreach (var fieldOffset in payload.FieldOffsets)
            {
                BufferBuilder.WriteUInt16(offset, fieldOffset.TypeId);
                offset += 2;
                BufferBuilder.WriteUInt16(offset, fieldOffset.Offset);
                offset += 2;
            }
        }

    }
}
