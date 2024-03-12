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
        private readonly IList<PatchPoint> _patches;
        public Builder(VirtualMemorySystem virtualMemory, ulong count)
        {
            _virtualMemory = virtualMemory;
            _buf = new byte[count];
            _patches = new List<PatchPoint>();
        }

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
            _patches.Clear();
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

        public void WriteExternalSizeT(int offset, VirtualMemorySystem.ExternalSizeT size)
        {
            _virtualMemory.WriteExternalSizeT(new Span<byte>(_buf, offset, _virtualMemory.PointerSize), size);
        }

        // Records a buffer offset in the current builder that will be filled in with a patch later
        public PatchPoint AddPatchPoint(int offset)
        {
            var patchPoint = new PatchPoint(this, offset);
            _patches.Add(patchPoint);
            return patchPoint;
        }

        private void ApplyPatches()
        {
            foreach (var patch in _patches)
            {
                patch.ApplyPatchPoint();
            }
        }

        // Create a patch that will fill in a patch point with the ExternalPtr value of the given offset, once the
        // sourceBuilder's start address is known
        public Patch MakeBufferOffsetAbsolutePointerPatch(Builder sourceBuilder, int offset) => new BufferOffsetToAbsolutePtrPatch(sourceBuilder, offset);

        public class PatchPoint
        {
            public readonly int PatchDest;
            public readonly Builder DestBuilder;
            private Patch? _patch;

            public PatchPoint(Builder destBuilder, int patchDest)
            {
                DestBuilder = destBuilder;
                PatchDest = patchDest;
            }
            public void SetPatch(Patch patch)
            {
                if (_patch != null)
                {
                    throw new InvalidOperationException("Patch already set");
                }
                _patch = patch;
            }

            public void ApplyPatchPoint()
            {
                if (_patch == null)
                {
                    throw new InvalidOperationException("Patch not set");
                }
                _patch.ApplyPatch(DestBuilder.Span.Slice(PatchDest, _patch.Size));
            }
        }

        public abstract class Patch
        {
            public enum PatchKind
            {
                SameBufferOffsetToAbsolutePtr, // given an offset in the current buffer, patch with the absolute address of that offset
            }

            protected Patch(PatchKind kind)
            {
                Kind = kind;
            }

            protected PatchKind Kind { get; }
            public abstract int Size { get; }

            public abstract void ApplyPatch(Span<byte> dest);
        }

        public class BufferOffsetToAbsolutePtrPatch : Patch
        {
            private readonly Builder _sourceBuilder;
            public BufferOffsetToAbsolutePtrPatch(Builder sourceBuilder, int offset) : base(Patch.PatchKind.SameBufferOffsetToAbsolutePtr)
            {
                _sourceBuilder = sourceBuilder;
                Offset = offset;
            }

            public int Offset { get; }

            public override int Size => _sourceBuilder._virtualMemory.PointerSize;
            public override void ApplyPatch(Span<byte> _buf)
            {
                var vms = _sourceBuilder._virtualMemory;
                var absPtr = vms.ToExternalPtr(_sourceBuilder._startAddr + (ulong)Offset);
                vms.WriteExternalPtr(_buf, absPtr);
            }
        }
    }


}
