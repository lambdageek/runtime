// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public abstract class VirtualAbstractStream : IVirtualMemoryRange
{
    public enum KnownStreams : ushort
    {
        Types,
        Blobs,
        Instances,
    }
    protected VirtualAbstractStream(VirtualMemorySystem virtualMemory, ushort id)
    {
        _virtualMemory = virtualMemory;
        Id = id;
    }

    public readonly ushort Id;
    private readonly VirtualMemorySystem _virtualMemory;
    protected VirtualMemorySystem VirtualMemory => _virtualMemory;

    public abstract ulong Start { get; }
    public abstract ulong Count { get; }

    // for lazy stream / data context initialization

    public abstract Patches.Patch StreamStartPatch { get; }

    public abstract Patches.Patch BlockDataSize { get; }

    public abstract Patches.Patch MaxDataSize { get; }

    public abstract bool TryReadExtent(ulong start, ulong count, Span<byte> buffer);

    public class MissingStream : VirtualAbstractStream
    {
        public MissingStream(VirtualMemorySystem virtualMemory, ushort id) : base(virtualMemory, id)
        {
            Start = virtualMemory.ToInternalPtr(virtualMemory.NullPointer);
            Count = (ulong)0u;
        }
        public override ulong Start { get; }
        public override ulong Count { get; }

        public override Patches.Patch StreamStartPatch => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.NullPointer);
        public override Patches.Patch BlockDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0u));

        public override Patches.Patch MaxDataSize => Patches.MakeConstPatch(VirtualMemory, VirtualMemory.ToExternalSizeT((ulong)0u));

        public override bool TryReadExtent(ulong start, ulong count, Span<byte> buffer) => false;
    }

    public abstract class StreamEntityWriter<T>
    {
        private readonly BufferBackedRange.Builder _bufBuilder;

        public StreamEntityWriter(BufferBackedRange.Builder bufBuilder)
        {
            _bufBuilder = bufBuilder;
        }

        protected BufferBackedRange.Builder BufferBuilder => _bufBuilder;

        public abstract int GetPayloadSize(T payload);
        public abstract void WritePayload(T payload, ref int offset);

        public int EntityHeaderSize => 2 * 4;

        public virtual void WriteEntity(T payload, ref int offset)
        {
            int size = EntityHeaderSize;
            int payloadSize = GetPayloadSize(payload);
            size += payloadSize;
            _bufBuilder.EnsureCapacity(offset, size);
            _bufBuilder.WriteUInt32(offset, (uint)size); // relative offset of next entity start
            offset += 4;
            _bufBuilder.WriteUInt32(offset, 0u); // reserved
            offset += 4;
            int oldOffset = offset;
            WritePayload(payload, ref offset);
            if (payloadSize != (offset - oldOffset))
            {
                throw new InvalidOperationException($"Payload size mismatch (payload {payloadSize} != written {offset - oldOffset})");
            }
        }
    }

}
