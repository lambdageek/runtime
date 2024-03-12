// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

// helper for various builders to lazily write into buffers once the size and start address are known
public static class Patches
{
    // Create a patch that will fill in a patch point with the ExternalPtr value of the given offset, once the
    // sourceBuilder's start address is known
    public static Patch MakeBufferOffsetAbsolutePointerPatch(BufferBackedRange.Builder sourceBuilder, int offset) => new BufferOffsetToAbsolutePtrPatch(sourceBuilder, offset);

    public static Patch MakeConstPatch(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalSizeT value)
    {
        return new ConstSizeTPatch(virtualMemory, value);
    }

    public static Patch MakeConstPatch(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalPtr value)
    {
        return new ConstPtrPatch(virtualMemory, value);
    }
    public class PatchPoint
    {
        public readonly int PatchDest;
        public readonly BufferBackedRange.Builder DestBuilder;
        private Patch? _patch;

        public PatchPoint(BufferBackedRange.Builder destBuilder, int patchDest)
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
            DestBuilder.ApplyPatch(_patch, PatchDest);
        }
    }

    public abstract class Patch
    {
        public enum PatchKind
        {
            SameBufferOffsetToAbsolutePtr, // given an offset in the current buffer, patch with the absolute address of that offset
            ConstSizeT,
            ConstPtr,
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
        private readonly BufferBackedRange.Builder _sourceBuilder;
        public BufferOffsetToAbsolutePtrPatch(BufferBackedRange.Builder sourceBuilder, int offset) : base(Patch.PatchKind.SameBufferOffsetToAbsolutePtr)
        {
            _sourceBuilder = sourceBuilder;
            Offset = offset;
        }

        public int Offset { get; }

        public override int Size => _sourceBuilder.VirtualMemory.PointerSize;
        public override void ApplyPatch(Span<byte> _buf)
        {
            var vms = _sourceBuilder.VirtualMemory;
            var absPtr = vms.Advance(_sourceBuilder.StartAddr, Offset);
            vms.WriteExternalPtr(_buf, absPtr);
        }
    }

    public class ConstSizeTPatch : Patch
    {
        private readonly VirtualMemorySystem _virtualMemory;
        private readonly VirtualMemorySystem.ExternalSizeT _value;
        public ConstSizeTPatch(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalSizeT value) : base(PatchKind.ConstSizeT)
        {
            _virtualMemory = virtualMemory;
            _value = value;
        }

        public override int Size => _virtualMemory.PointerSize;
        public override void ApplyPatch(Span<byte> _buf)
        {
            _virtualMemory.WriteExternalSizeT(_buf, _value);
        }
    }

    public class ConstPtrPatch : Patch
    {
        private readonly VirtualMemorySystem _virtualMemory;
        private readonly VirtualMemorySystem.ExternalPtr _value;
        public ConstPtrPatch(VirtualMemorySystem virtualMemory, VirtualMemorySystem.ExternalPtr value) : base(PatchKind.ConstPtr)
        {
            _virtualMemory = virtualMemory;
            _value = value;
        }

        public override int Size => _virtualMemory.PointerSize;
        public override void ApplyPatch(Span<byte> _buf)
        {
            _virtualMemory.WriteExternalPtr(_buf, _value);
        }
    }
}
