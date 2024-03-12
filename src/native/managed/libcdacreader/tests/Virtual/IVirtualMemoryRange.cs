// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

// Represents a range of virtual memory
public interface IVirtualMemoryRange
{
    // The start address of the range, in the system's logical address space
    ulong Start { get; }
    // The number of bytes in the range
    ulong Count { get; }
    // Read a range of bytes from the range, starting at the given offset
    // returns false if the range does not contain the given offset and count
    bool TryReadExtent(ulong start, ulong count, Span<byte> buffer);

    static bool DoesNotIntersect(IVirtualMemoryRange first, IVirtualMemoryRange second)
    {
        ulong firstEnd = first.Start + first.Count;
        ulong secondEnd = second.Start + second.Count;
        return firstEnd <= second.Start || secondEnd <= first.Start;
    }
    static bool Overlaps(IVirtualMemoryRange first, IVirtualMemoryRange second)
    {
        return !DoesNotIntersect(first, second);
    }

}

// Represents a range of virtual memory that is owned by a particular subsystem - can be added to the overall VirutlaMemorySystem.
public interface IVirtualMemoryRangeOwner : IVirtualMemoryRange, IComparable<IVirtualMemoryRangeOwner>
{
    int IComparable<IVirtualMemoryRangeOwner>.CompareTo(IVirtualMemoryRangeOwner? other)
    {
        if (other == null)
            return 1;
        if (Overlaps(this, other))
            throw new InvalidOperationException("Ranges overlap");
        return Start.CompareTo(other.Start);
    }
}
