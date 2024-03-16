// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public class VirtualBlobStream : VirtualBufferBackedStream, IVirtualMemoryRangeOwner
{
    public struct BlobEntity
    {
        public VirtualTypeStream.TypeDetails Type { get; init; }
        public ushort Size { get; init; }
        public ReadOnlyMemory<byte> Data { get; init; } // TODO: something more flexible? and/or target-arch aware?
    }
    private readonly IReadOnlyCollection<BlobEntity> _blobs;
    private VirtualBlobStream(VirtualMemorySystem virtualMemory, BufferBackedRange buffer, IReadOnlyCollection<BlobEntity> blobs) : base(virtualMemory, KnownStream.Blobs, buffer)
    {
        _blobs = blobs;
    }

    public class BlobStreamBuilder : Builder<VirtualBlobStream>
    {
        private readonly IReadOnlyCollection<BlobEntity> _blobs;
        private readonly BlobEntityWriter _writer;
        public BlobStreamBuilder(VirtualMemorySystem virtualMemory, IReadOnlyCollection<BlobEntity> blobs = default) : base(virtualMemory, KnownStream.Blobs)
        {
            if (blobs == default)
            {
                blobs = Array.Empty<BlobEntity>();
            }
            _blobs = blobs;
            _writer = new BlobEntityWriter(BufBuilder);
        }
        protected override VirtualBlobStream CreateInstance(BufferBackedRange buf) => new VirtualBlobStream(VirtualMemory, buf, _blobs);

        protected override void InitDataBlockContentEntities(ref int offset)
        {
            foreach (var blob in _blobs)
            {
                _writer.WriteEntity(blob, ref offset);
            }
        }

        // potentially need to override InitDataBlockContent to make pos point to some real memory and curr/end to point after it.
        // not sure if it's ok for the data block to be completely empty.

    }

    public class BlobEntityWriter : VirtualAbstractStream.StreamEntityWriter<BlobEntity>
    {
        public BlobEntityWriter(BufferBackedRange.Builder bufBuilder) : base(bufBuilder) { }
        public override int GetPayloadSize(BlobEntity payload)
        {
            return 2 * 2 + payload.Data.Length;
        }
        public override void WritePayload(BlobEntity payload, ref int offset)
        {
            int size = GetPayloadSize(payload);
            BufferBuilder.EnsureCapacity(offset, size);
            BufferBuilder.WriteUInt16(offset, payload.Type.Id);
            offset += 2;
            BufferBuilder.WriteUInt16(offset, payload.Type.Version);
            offset += 2;
            BufferBuilder.WriteBytes(offset, payload.Data.Span);
            offset += payload.Data.Length;
        }
    }

}
