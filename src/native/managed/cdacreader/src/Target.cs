// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Text;

namespace Microsoft.Diagnostics.DataContractReader;

public struct TargetPointer
{
    public static TargetPointer Null = new(0);

    public ulong Value;
    public TargetPointer(ulong value) => Value = value;
}

internal sealed unsafe class Target
{
    private readonly delegate* unmanaged<ulong, byte*, uint, void*, int> _readFromTarget;
    private readonly void* _readContext;

    private bool _isLittleEndian;
    private int _pointerSize;

    public Target(ulong contractDescriptorAddr, delegate* unmanaged<ulong, byte*, uint, void*, int> readFromTarget, void* readContext)
    {
        _readFromTarget = readFromTarget;
        _readContext = readContext;

        if (!TryReadDescriptor(contractDescriptorAddr, out _isLittleEndian, out _pointerSize))
            throw new InvalidOperationException("Failed to read descriptor");
        // TODO: parse json

    }

    [Flags]
    private enum PlatformFlagsBits : uint
    {
        Bit0 = 1, // reserved bit always set
        PointerSizeBit = 2, // 0 = 8 bytes, 1 = 4 bytes
    }

    private bool TryReadDescriptor(ulong address, out bool isLittleEndian, out int pointerSize)
    {
        isLittleEndian = false;
        pointerSize = 0;
        byte* buffer = stackalloc byte[8];
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, 8);
        if (ReadFromTarget(address, buffer, 8) < 0)
        {
            return false;
        }
        if (BinaryPrimitives.ReadUInt64LittleEndian(span) == 0x0043414443434e44ul)
        {
            Console.Error.WriteLine("little endian descriptor");
            isLittleEndian = true;
        }
        else if (BinaryPrimitives.ReadUInt64BigEndian(span) == 0x0043414443434e44ul)
        {
            Console.Error.WriteLine("big endian descriptor");
            isLittleEndian = false;
        }
        else
        {
            Console.Error.WriteLine("coudl not parse magic");
            return false;
        }
        address += sizeof(ulong); // advance to platform flags
        if (ReadFromTarget(address, buffer, 4) < 0)
        {
            return false;
        }
        PlatformFlagsBits platformFlags = isLittleEndian
            ? (PlatformFlagsBits)BinaryPrimitives.ReadUInt32LittleEndian(span)
            : (PlatformFlagsBits)BinaryPrimitives.ReadUInt32BigEndian(span);
        if ((platformFlags & PlatformFlagsBits.Bit0) != PlatformFlagsBits.Bit0)
        {
            Console.Error.WriteLine("not a valid descriptor: platformFlags bit0 is 0");
            return false;
        }
        if ((platformFlags & PlatformFlagsBits.PointerSizeBit) == PlatformFlagsBits.PointerSizeBit)
        {
            pointerSize = 8;
        }
        else
        {
            pointerSize = 4;
        }

        address += sizeof(uint); // advance to json length

        if (ReadFromTarget(address, buffer, 4) < 0)
        {
            return false;
        }
        uint jsonPayloadLength = isLittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(span)
            : BinaryPrimitives.ReadUInt32BigEndian(span);
        Console.Error.WriteLine($"json payload length: {jsonPayloadLength}");
        address += sizeof(uint); // advance to json payload pointer
        if (ReadFromTarget(address, buffer, 8) < 0)
        {
            return false;
        }
        ulong jsonPayloadAddress = isLittleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(span)
            : BinaryPrimitives.ReadUInt64BigEndian(span);
        Console.Error.WriteLine($"json payload address: 0x{jsonPayloadAddress:x16}");
        address += sizeof(ulong); // advance to pointer data count
        if (ReadFromTarget(address, buffer, 4) < 0)
        {
            return false;
        }
        uint pointerDataCount = isLittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(span)
            : BinaryPrimitives.ReadUInt32BigEndian(span);
        Console.Error.WriteLine($"pointer data count: {pointerDataCount}");
        address += sizeof(uint); // advance to padding;
        address += sizeof(uint); // advance to pointer data address
        if (ReadFromTarget(address, buffer, 8) < 0)
        {
            return false;
        }
        ulong pointerDataAddress = isLittleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(span)
            : BinaryPrimitives.ReadUInt64BigEndian(span);
        Console.Error.WriteLine($"pointer data address: 0x{pointerDataAddress:x16}");
        byte[] jsonBuffer = new byte[jsonPayloadLength];
        fixed (byte* jsonBufferPtr = jsonBuffer)
        {
            if (ReadFromTarget(jsonPayloadAddress, jsonBufferPtr, jsonPayloadLength) < 0)
            {
                return false;
            }
        }
        string s = Encoding.ASCII.GetString(jsonBuffer);
        Console.Error.WriteLine($"json: {s}");
        ContractDescriptorParser.ContractDescriptor? descriptor = ContractDescriptorParser.ParseCompact(jsonBuffer);
        if (descriptor == null)
        {
            Console.Error.WriteLine("failed to parse descriptor");
            return false;
        }
        Console.Error.WriteLine($"parsed descriptor: {descriptor}");
        return true;
    }

    public bool TryReadPointer(ulong address, out TargetPointer pointer)
    {
        pointer = TargetPointer.Null;

        byte* buffer = stackalloc byte[_pointerSize];
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer, _pointerSize);
        if (ReadFromTarget(address, buffer, (uint)_pointerSize) < 0)
            return false;

        if (_pointerSize == sizeof(uint))
        {
            pointer = new TargetPointer(
                _isLittleEndian
                    ? BinaryPrimitives.ReadUInt32LittleEndian(span)
                    : BinaryPrimitives.ReadUInt32BigEndian(span));
        }
        else if (_pointerSize == sizeof(ulong))
        {
            pointer = new TargetPointer(
                _isLittleEndian
                    ? BinaryPrimitives.ReadUInt64LittleEndian(span)
                    : BinaryPrimitives.ReadUInt64BigEndian(span));
        }

        return true;
    }

    private int ReadFromTarget(ulong address, byte* buffer, uint bytesToRead)
        => _readFromTarget(address, buffer, bytesToRead, _readContext);
}
