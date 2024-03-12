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
    public class Builder
    {
        private readonly VirtualMemorySystem _virtualMemory;
        private ulong _startAddr;
        private byte[] _buf;
        private readonly IList<Patches.PatchPoint> _patchPoints;
        public Builder(VirtualMemorySystem virtualMemory, int count)
        {
            _virtualMemory = virtualMemory;
            _buf = new byte[count];
            _patchPoints = new List<Patches.PatchPoint>();
        }

        public VirtualMemorySystem VirtualMemory => _virtualMemory;
        public VirtualMemorySystem.ExternalPtr StartAddr => _virtualMemory.ToExternalPtr(_startAddr);

        public void SetStart(VirtualMemorySystem.ExternalPtr startAddr)
        {
            if (_startAddr != (ulong)0u)
            {
                throw new InvalidOperationException("Start address already set");
            }
            _startAddr = _virtualMemory.ToInternalPtr(startAddr);
        }

        public ulong GetRequestedSize() => (ulong)_buf.Length;

        public BufferBackedRange Create()
        {
            if (_startAddr == (ulong)0u)
            {
                throw new InvalidOperationException("Start address not set for buffer backed range");
            }
            ApplyPatches();
            byte[] buf = _buf;
            _patchPoints.Clear();
            _buf = null;
            return new BufferBackedRange(_startAddr, new Memory<byte>(buf));
        }

        // a bit dangerous  to do `Span<byte> otherSpan = ...; otherSpan.CopyTo(builder.Span)` - make sure endianness is matched in the other buffer
        private Span<byte> Span => new Span<byte>(_buf);

        public void EnsureCapacity(int offset, int size)
        {
            if (offset + size >= _buf.Length)
            {
                GrowTo(offset + size);
            }
        }

        private void GrowTo(int newSize)
        {
            byte[] newBuf = new byte[newSize];
            _buf.CopyTo(newBuf.AsSpan());
            _buf = newBuf;
        }

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

        public void WriteExternalSizeT(int offset, VirtualMemorySystem.ExternalSizeT size)
        {
            _virtualMemory.WriteExternalSizeT(new Span<byte>(_buf, offset, _virtualMemory.PointerSize), size);
        }

        // Records a buffer offset in the current builder that will be filled in with a patch later
        public Patches.PatchPoint AddPatchPoint(int offset)
        {
            var patchPoint = new Patches.PatchPoint(this, offset);
            _patchPoints.Add(patchPoint);
            return patchPoint;
        }

        internal void ApplyPatch(Patches.Patch patch, int offset)
        {
            patch.ApplyPatch(Span.Slice(offset, patch.Size));
        }

        private void ApplyPatches()
        {
            foreach (var pp in _patchPoints)
            {
                pp.ApplyPatchPoint();
            }
        }

    }
}
