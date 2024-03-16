// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public class VirtualTypeStream : VirtualBufferBackedStream, IVirtualMemoryRangeOwner
{
    private readonly IReadOnlyCollection<TypeEntity> _entities;

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

    private VirtualTypeStream(VirtualMemorySystem virtualMemory, BufferBackedRange buffer, IReadOnlyList<TypeEntity> entities) : base(virtualMemory, KnownStream.Types, buffer)
    {
        _entities = entities;
    }


    public class TypeStreamBuilder : VirtualAbstractStream.Builder<VirtualTypeStream>
    {
        private readonly IReadOnlyList<TypeEntity> _entities;
        private readonly TypeEntityWriter _typeEntityWriter;
        private readonly Patches.PatchPoint[] _typeDetailPatchPoints;

        public TypeStreamBuilder(VirtualMemorySystem virtualMemory, TypeEntity[] entities) : base(virtualMemory, KnownStream.Types)
        {
            _entities = entities;
            _typeEntityWriter = new TypeEntityWriter(VirtualMemory, BufBuilder);
            _typeDetailPatchPoints = new Patches.PatchPoint[entities.Length];
        }


        protected override VirtualTypeStream CreateInstance(BufferBackedRange buf) => new VirtualTypeStream(VirtualMemory, buf, _entities);

        protected override void InitDataBlockContentEntities(ref int offset)
        {
            int idx = 0;
            int startOffset = offset;
            foreach (var entity in _entities)
            {
                _typeEntityWriter.WriteEntity(entity, ref offset);
                _typeDetailPatchPoints[idx++] = _typeEntityWriter.LastTypeDetailsPatchPoint; // yuck
            }
        }

        protected override void InitAdditionalMemoryContent(ref int offset)
        {
            Patches.PatchPoint[] typeNamePatchPoints = new Patches.PatchPoint[_entities.Count];
            InitTypeDetails(ref offset, _entities, _typeDetailPatchPoints, typeNamePatchPoints);
            InitTypeNames(ref offset, _entities, typeNamePatchPoints);
        }

        private void InitTypeDetails(ref int offset, IReadOnlyList<TypeEntity> entities, IReadOnlyList<Patches.PatchPoint> typeDetailPatchPoints, Patches.PatchPoint[] namesPatchPoints)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                var patchPoint = typeDetailPatchPoints[i];
                // patch up the type entity to point to the type details we're about to write
                patchPoint.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(BufBuilder, offset));
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

        public void WriteTypeDetailsPayload(TypeDetails entity, ref int offset, out Patches.PatchPoint namePointerPatch)
        {
            BufBuilder.EnsureCapacity(offset, GetTypeDetailsSize(entity));
            BufBuilder.WriteUInt16(offset, entity.Id);
            offset += 2;
            BufBuilder.WriteUInt16(offset, entity.Version);
            offset += 2;
            BufBuilder.WriteUInt16(offset, 0); // reserved
            offset += 2;
            int byteCount = Encoding.UTF8.GetByteCount(entity.Name) + 1; // include nul
            BufBuilder.WriteUInt16(offset, (ushort)byteCount); // name_len
            offset += 2;
            BufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
            namePointerPatch = BufBuilder.AddPatchPoint(offset);
            offset += VirtualMemory.PointerSize;
        }

        public void WriteTypeName(ref int offset, string name, Patches.PatchPoint namePointerPatch)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            BufBuilder.EnsureCapacity(offset, bytes.Length + 1);
            namePointerPatch.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(BufBuilder, offset));
            BufBuilder.WriteBytes(offset, bytes);
            offset += bytes.Length + 1; // include null terminator
        }

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

    }

    class TypeEntityWriter : StreamEntityWriter<TypeEntity>
    {
        VirtualMemorySystem _virtualMemory;
        public TypeEntityWriter(VirtualMemorySystem virtualMemory, BufferBackedRange.Builder bufBuilder) : base(bufBuilder)
        {
            _virtualMemory = virtualMemory;
        }

        // hack: WritePayload sets this to the location where the pointer to the type details should go
        public Patches.PatchPoint LastTypeDetailsPatchPoint { get; private set; }

        public static int FieldOffsetSize => 4; // 2*uint16_t

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
