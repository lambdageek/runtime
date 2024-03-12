// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
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
}
