// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public abstract class VirtualBufferBackedStream : VirtualAbstractStream, IVirtualMemoryRangeOwner
{
    private BufferBackedRange _buf;

    protected VirtualBufferBackedStream(VirtualMemorySystem virtualMemory, KnownStream id, BufferBackedRange buffer) : base(virtualMemory, id)
    {
        _buf = buffer;
    }
    protected virtual BufferBackedRange Buffer => _buf;

    public override ulong Start => _buf.Start;
    public override ulong Count => _buf.Count;

    public override bool TryReadExtent(ulong start, ulong count, Span<byte> buffer) => _buf.TryReadExtent(start, count, buffer);

}
