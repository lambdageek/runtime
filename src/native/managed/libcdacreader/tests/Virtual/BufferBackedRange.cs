// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

// helper class that represents a range of virtual memory that is backed by an array of bytes
public partial class BufferBackedRange : IVirtualMemoryRange
{
    // takes ownership of the buffer
    private BufferBackedRange(ulong start, ReadOnlyMemory<byte> buffer)
    {
        Start = start;
        _buffer = buffer;
    }

    public bool TryReadExtent(ulong start, Span<byte> dest)
    {
        if (start < Start || start + (ulong)dest.Length >= Start + Count)
        {
            return false;
        }
        _buffer.Span.Slice((int)(start - Start), dest.Length).CopyTo(dest);
        return true;
    }

    public static BufferBackedRange.Builder Build(VirtualMemorySystem virtualMemory, int count)
    {
        return new Builder(virtualMemory, count);
    }

    private readonly ReadOnlyMemory<byte> _buffer;

    public ulong Start { get; }
    public ulong Count => (ulong)_buffer.Length;

    public bool TryReadExtent(ulong start, ulong count, Span<byte> buffer)
    {
        if (start < Start || start + count > Start + Count)
            return false;
        _buffer.Span.Slice((int)(start - Start), (int)count).CopyTo(buffer);
        return true;
    }
}
