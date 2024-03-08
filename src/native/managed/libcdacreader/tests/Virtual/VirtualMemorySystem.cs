// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

// TODO: maybe we care about alignment?
public class VirtualMemorySystem
{
    private readonly bool _oppositeEndian;
    private readonly int _pointerSize; // in bytes
    private readonly ulong _maxPointerValue;
    private readonly SortedSet<IVirtualMemoryRange> _ranges = new(); // sorted by start address

    // this is a pointer the way it would be viewed in memory from the outside
    // this is an opaque representation and can't be used for pointer arithmetic
    public struct ExternalPtr
    {
        internal readonly ulong Value;
        public ExternalPtr(ulong value)
        {
            Value = value;
        }
        public override string ToString() => $"ExternalPtr(0x{Value:x})";
    }

    public VirtualMemorySystem(bool isLittleEndian, int pointerSize)
    {
        if (BitConverter.IsLittleEndian == isLittleEndian)
            _oppositeEndian = false;
        else
            _oppositeEndian = true;
        _pointerSize = pointerSize;
        _maxPointerValue = (ulong)Math.Pow(2, 8 * pointerSize) - 1;

    }

    // reserve the low addresses so it's never valid to read from them
    public class NullPage : IVirtualMemoryRange
    {
        public ulong Start => 0;
        public ulong Count => 0x1000;
        public bool TryReadExtent(ulong start, ulong count, Span<byte> buffer)
        {
            return false;
        }
    }

    public int PointerSize => _pointerSize;

    public bool IsValidPointer(ExternalPtr pointerValue)
    {
        return ToInternalPtr(pointerValue) <= _maxPointerValue;
    }

    public bool OpposizeEndian => _oppositeEndian;
    public bool IsLittleEndian => BitConverter.IsLittleEndian == !_oppositeEndian;

    public void ValidatePointer(ExternalPtr pointerValue)
    {
        if (!IsValidPointer(pointerValue))
            throw new ArgumentOutOfRangeException(nameof(pointerValue), "Pointer value is not valid for this context");
    }

    // convert an internal pointer to an external view - these are opaque and can't be used for pointer arithmetic
    ExternalPtr ToExternalPtr(ulong value)
    {
        if (_oppositeEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }
        return new ExternalPtr(value);
    }

    // Dangerous!
    public ExternalPtr MakeExternalPtrRaw(ulong value)
    {
        return ToExternalPtr(value);
    }

    // convert to the logical address space of the system - it's valid to do pointer arithmetic on these
    internal ulong ToInternalPtr(ExternalPtr pointerValue)
    {
        if (_oppositeEndian)
        {
            return BinaryPrimitives.ReverseEndianness(pointerValue.Value);
        }
        return pointerValue.Value;
    }

    // dangerous!
    public ulong ToRawValue(ExternalPtr pointerValue)
    {
        return pointerValue.Value;
    }

    internal ExternalPtr Advance(ExternalPtr pointerValue, ulong count)
    {
        return ToExternalPtr(ToInternalPtr(pointerValue) + count);
    }

    internal void AddRange(IVirtualMemoryRange range)
    {
        _ranges.Add(range);
    }


    public void AddNullPage()
    {
        AddRange(new NullPage());
    }

    internal bool TryFindContainingRange(ExternalPtr pointerValue, out IVirtualMemoryRange? range)
    {
        range = null;
        if (!IsValidPointer(pointerValue))
            return false;
        ulong address = ToInternalPtr(pointerValue);
        range = _ranges.FirstOrDefault(r => r.Start <= address && address < r.Start + r.Count);
        return range != null;
    }

    public bool TryReadUInt32(ExternalPtr address, out uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        if (!TryRead(address, 4, buffer))
        {
            value = 0;
            return false;
        }
        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        if (_oppositeEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        return true;
    }

    public bool TryRead(ExternalPtr pointerValue, ulong count, Span<byte> buffer)
    {
        while (count > 0)
        {
            if (!TryFindContainingRange(pointerValue, out IVirtualMemoryRange? range))
                return false;
            ulong startAddress = ToInternalPtr(pointerValue);
            ulong offset = startAddress - range.Start;
            // how much to read from the current range
            ulong localCount = Math.Min(count, range.Count - offset);
            if (!range.TryReadExtent(startAddress, localCount, buffer))
                return false;
            // try to read the rest of the count from the next range
            count -= localCount;
            buffer = buffer.Slice((int)localCount);
            pointerValue = Advance(pointerValue, localCount);
        }
        return true;
    }

    internal void WriteUInt16(Span<byte> dest, ushort value)
    {
        if (IsLittleEndian)
            BinaryPrimitives.WriteUInt16LittleEndian(dest, value);
        else
            BinaryPrimitives.WriteUInt16BigEndian(dest, value);
    }

    internal void WriteUInt32(Span<byte> dest, uint value)
    {
        if (IsLittleEndian)
            BinaryPrimitives.WriteUInt32LittleEndian(dest, value);
        else
            BinaryPrimitives.WriteUInt32BigEndian(dest, value);
    }

    internal void WriteUInt64(Span<byte> dest, ulong value)
    {
        if (IsLittleEndian)
            BinaryPrimitives.WriteUInt64LittleEndian(dest, value);
        else
            BinaryPrimitives.WriteUInt64BigEndian(dest, value);
    }

    internal void WriteExternalPtr(Span<byte> dest, ExternalPtr value)
    {
        ValidatePointer(value);
        if (PointerSize == 4)
            WriteUInt32(dest, (uint)ToInternalPtr(value));
        else
            WriteUInt64(dest, ToInternalPtr(value));
    }

    public ExternalPtr NullPointer => new ExternalPtr(0);


    // find a free address range in the system's logical address space big enough for the given size
    internal ulong FindFreeAddress(ulong size)
    {
        ulong lastEnd = 0;
        foreach (IVirtualMemoryRange range in _ranges)
        {
            if (range.Start - lastEnd >= size)
                return lastEnd;
            lastEnd = range.Start + range.Count;
        }
        return lastEnd;
    }

    public DataContractReaderReaderCallback CreateReaderCallback()
    {
        return new DataContractReaderReaderCallback(this);
    }

    public class DataContractReaderReaderCallback : IDisposable
    {
        internal DataContractReaderReaderCallback(VirtualMemorySystem virtualMemory)
        {
            _virtualMemory = virtualMemory;
            _gcHandle = GCHandle.Alloc(this);
        }
        private readonly VirtualMemorySystem _virtualMemory;
        private readonly GCHandle _gcHandle;

        public IntPtr UserData => GCHandle.ToIntPtr(_gcHandle);

        public void Dispose()
        {
            _gcHandle.Free();
        }

        public bool TryRead(ExternalPtr address, uint count, Span<byte> buffer)
        {
            return _virtualMemory.TryRead(address, count, buffer);
        }
    }

    [UnmanagedCallersOnly]
    public static unsafe int Reader(ulong address, uint count, IntPtr userData, byte* buffer)
    {
        GCHandle h = GCHandle.FromIntPtr(userData);
        DataContractReaderReaderCallback? callback = h.Target as DataContractReaderReaderCallback;
        if (callback == null)
            return -1;
        Span<byte> span = new Span<byte>(buffer, (int)count);
        if (!callback.TryRead(new VirtualMemorySystem.ExternalPtr(address), count, span))
            return -1;
        return 0;
    }


}
