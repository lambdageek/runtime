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
    protected VirtualAbstractStream(VirtualMemorySystem virtualMemory, ushort id, VirtualMemorySystem.ExternalPtr start, ulong count)
    {
        _virtualMemory = virtualMemory;
        Start = virtualMemory.ToInternalPtr(start);
        Count = count;
        Id = id;
    }

    public readonly ushort Id;
    private readonly VirtualMemorySystem _virtualMemory;
    protected VirtualMemorySystem VirtualMemory => _virtualMemory;

    public abstract ulong Start { get; protected init; }
    public abstract ulong Count { get; protected init; }

    public abstract bool TryReadExtent(ulong start, ulong count, Span<byte> buffer);

}
