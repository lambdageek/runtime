// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public abstract partial class VirtualAbstractStream
{
    public abstract class Builder
    {
        public abstract KnownStream Id { get; }
        protected abstract VirtualMemorySystem VirtualMemory { get; }
        public abstract Patches.Patch StreamStartPatch { get; }

        public virtual Patches.Patch MaxDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0));
        public virtual Patches.Patch BlockDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0));

        public abstract void Build();
    }
    public abstract class Builder<T> : Builder where T : VirtualAbstractStream, IVirtualMemoryRangeOwner
    {
        private readonly VirtualMemorySystem _virtualMemory;
        protected readonly KnownStream _id;
        private readonly BufferBackedRange.Builder _bufBuilder;
        private Patches.Patch _dataBlockStart;

        public Builder(VirtualMemorySystem virtualMemory, KnownStream id)
        {
            _virtualMemory = virtualMemory;
            _id = id;
            _bufBuilder = new BufferBackedRange.Builder(virtualMemory, 16); // just the header, for now
        }


        public override KnownStream Id => _id;
        protected override VirtualMemorySystem VirtualMemory => _virtualMemory;

        protected BufferBackedRange.Builder BufBuilder => _bufBuilder;

        public override Patches.Patch StreamStartPatch => _dataBlockStart;


        protected abstract T CreateInstance(BufferBackedRange buf);

        // factory method to create the stream and populate it with data
        // returns a patch that will resolve to the address of the start of the head sstream data block
        public override void Build()
        {
            int offset = 0;
            var dataBlockPatchPoints = InitDataBlock(ref offset, out Patches.Patch startPatch);
            _dataBlockStart = startPatch;
            InitDataBlockContent(ref offset, dataBlockPatchPoints);
            InitAdditionalMemoryContent(ref offset);
            _virtualMemory.Reservations.Add()
                .OnGetRequetedSize(BufBuilder.GetRequestedSize)
                .OnSetStartAddress(BufBuilder.SetStart)
                .OnReservationsComplete(() =>
                {
                    var stream = CreateInstance(BufBuilder.Create());
                    _virtualMemory.AddRange(stream);
                });
        }

        private DataBlockPatchPoints InitDataBlock(ref int offset, out Patches.Patch dataBlockStart)
        {
            BufBuilder.EnsureCapacity(offset, 4 * VirtualMemory.PointerSize);
            dataBlockStart = Patches.MakeBufferOffsetAbsolutePointerPatch(BufBuilder, offset);
            BufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
            var ppBegin = BufBuilder.AddPatchPoint(offset);
            offset += VirtualMemory.PointerSize;
            BufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
            var ppPos = BufBuilder.AddPatchPoint(offset);
            offset += VirtualMemory.PointerSize;
            BufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
            var ppEnd = BufBuilder.AddPatchPoint(offset);
            offset += VirtualMemory.PointerSize;
            BufBuilder.WriteExternalPtr(offset, VirtualMemory.NullPointer);
            var ppPrev = BufBuilder.AddPatchPoint(offset);
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
            // FIXME:
            //   Blocks are filled in reverse order (`end` to `begin`) to ensure reading of a stream is always performed in reverse chronological order.
            patchPoints.Begin.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(BufBuilder, offset));
            patchPoints.Pos.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(BufBuilder, offset));
            InitDataBlockContentEntities(ref offset);
            patchPoints.End.SetPatch(Patches.MakeBufferOffsetAbsolutePointerPatch(BufBuilder, offset));
            patchPoints.Prev.SetPatch(Patches.MakeConstPatch(VirtualMemory, VirtualMemory.NullPointer));
        }

        // subclasses should override to add the individual stream entities
        protected virtual void InitDataBlockContentEntities(ref int offset)
        {
            // TODO: use the stream entity writer to add entities to the stream
        }

        // subclasses can override to add more data to the stream after the data blocks
        protected virtual void InitAdditionalMemoryContent(ref int offset)
        {
        }

        readonly struct DataBlockPatchPoints
        {
            public Patches.PatchPoint Begin { get; init; }
            public Patches.PatchPoint Pos { get; init; }
            public Patches.PatchPoint End { get; init; }
            public Patches.PatchPoint Prev { get; init; }
        }


    }
}
