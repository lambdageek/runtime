// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

// helper class that represents a range of virtual memory that is backed by an array of bytes
public class BufferBackedRange : IVirtualMemoryRange
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

    public class Builder
    {
        private readonly VirtualMemorySystem _virtualMemory;
        private ulong _startAddr;
        private byte[] _buf;
        public Builder(VirtualMemorySystem virtualMemory, ulong count)
        {
            _virtualMemory = virtualMemory;
            _buf = new byte[count];
        }

        public void SetStart(VirtualMemorySystem.ExternalPtr startAddr)
        {
            if (_startAddr != (ulong)0u)
            {
                throw new InvalidOperationException("Start address already set");
            }
            _startAddr = _virtualMemory.ToInternalPtr(startAddr);
        }

        public BufferBackedRange Create()
        {
            if (_startAddr == (ulong)0u)
            {
                throw new InvalidOperationException("Start address not set for buffer backed range");
            }
            byte[] buf = _buf;
            _buf = null;
            return new BufferBackedRange(_startAddr, new Memory<byte>(buf));
        }

        // a bit dangerous  to do `Span<byte> otherSpan = ...; otherSpan.CopyTo(builder.Span)` - make sure endianness is matched in the other buffer
        private Span<byte> Span => new Span<byte>(_buf);

        public void WriteUInt8(int offset, byte b)
        {
            _buf[offset] = b;
        }

        public void WriteUInt16(int offset, ushort s)
        {
            _virtualMemory.WriteUInt16(new Span<byte>(_buf, offset, 2), s);
        }
        public void WriteUInt32(int offset, uint i)
        {
            _virtualMemory.WriteUInt32(new Span<byte>(_buf, offset, 4), i);
        }

        public void WriteExternalPtr(int offset, VirtualMemorySystem.ExternalPtr ptr)
        {
            _virtualMemory.WriteExternalPtr(new Span<byte>(_buf, offset, _virtualMemory.PointerSize), ptr);
        }
    }

    public static BufferBackedRange.Builder Build(VirtualMemorySystem virtualMemory, ulong count)
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
