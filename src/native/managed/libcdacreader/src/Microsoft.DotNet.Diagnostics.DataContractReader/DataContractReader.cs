// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader;

public sealed class DataContractReader : IDisposable
{
    public const int Magic = 0x646e6300;
    public static readonly ReadOnlyMemory<byte> MagicBE = new byte[] {0x64, 0x6e, 0x63, 0x00};
    public static readonly ReadOnlyMemory<byte> MagicLE = new byte[] {0x00, 0x63, 0x6e, 0x64};

    public struct TypeDetails
    {
        public TypeDetails() { }

        public Dictionary<ushort, byte[]> Blobs = new Dictionary<ushort, byte[]>();
    }

    private TypeDetails Details { get; } = new TypeDetails();
    internal RemoteConfig Config { get; private set; }

    public DataContractReader()
    {
        Console.Error.WriteLine("DataContractReader created!");
    }

    public void Dispose()
    {
        _reader = default;
    }

    public Span<byte> GetBlob(ushort id)
    {
        if (!Details.Blobs.TryGetValue(id, out byte[]? blob))
            throw new InvalidOperationException("Blob not found");

        return blob;
    }

    internal unsafe void SetReaderFunc(delegate* unmanaged<ulong, uint, IntPtr, byte*, int> readerFunc, IntPtr userData)
    {
        _reader = new ReaderFunc(readerFunc, userData);
    }

    internal unsafe void SetStream(nuint dataStreamAddress)
    {
        _stream = new ForeignPtr(dataStreamAddress);
        Console.WriteLine ("Stream starts at 0x{0:x}", Stream.Value);
        // TODO: move to a ValidateContext() function
        Span<byte> magicSpan = stackalloc byte[4];
        if (!Reader.Read(Stream, magicSpan)) {
            throw new Exception("couldn't read magic");
        }
        bool isLittleEndian;
        if (magicSpan.SequenceEqual(MagicLE.Span))
        {
            isLittleEndian = true;
        } else if (magicSpan.SequenceEqual(MagicBE.Span))
        {
            isLittleEndian = false;
        } else {
            Console.WriteLine ("Expected magic, got 0x{0:x} 0x{1:x} 0x{2:x} 0x{3:x}", magicSpan[0], magicSpan[1], magicSpan[2], magicSpan[3]);
            throw new Exception ("incorrect magic value");
        }

        Console.WriteLine ("target is {0}", isLittleEndian ? "LE" : "BE");

        ForeignU32 magic = _reader.ReadU32(_stream);

        DataStream.ds_validate_t endian = DataStream.dnds_validate(magic.Value);
        if (endian == DataStream.ds_validate_t.dsv_invalid)
            throw new InvalidOperationException("Corrupt data stream");

        Span<byte> dest = stackalloc byte[sizeof(ushort)];
        if (!_reader.Read(new ForeignPtr(dataStreamAddress + sizeof(uint)), dest))
            throw new InvalidOperationException("Failed to read context size");

        Config = new RemoteConfig()
        {
            IsLittleEndian = endian == DataStream.ds_validate_t.dsv_little_endian
        };

        ushort cxtSize = endian == DataStream.ds_validate_t.dsv_big_endian
            ? BinaryPrimitives.ReadUInt16BigEndian(dest)
            : BinaryPrimitives.ReadUInt16LittleEndian(dest);

        byte[]? cxt = _reader.Read(new ForeignPtr(dataStreamAddress), cxtSize);

        fixed (byte* cxtPtr = cxt)
        fixed(ReaderFunc* readerFunc = &_reader)
        {
            DataStream.memory_reader_t reader;
            reader.read_ptr = &ReadMemory;
            reader.free_ptr = &FreeMemory;
            reader.context = readerFunc;

            // [TODO: cDAC] Enumerate types and instances

            var handle = GCHandle.Alloc(this);
            if (!DataStream.dnds_enum_blobs(cxtPtr, &OnNextBlob, GCHandle.ToIntPtr(handle), &reader))
                throw new InvalidOperationException("Failed to enumerate blobs");

            handle.Free();
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe byte OnNextBlob(ushort type, ushort size, void* data, IntPtr user_data)
    {
        GCHandle handle = GCHandle.FromIntPtr(user_data);
        DataContractReader? reader = handle.Target as DataContractReader;
        if (reader == null)
            throw new InvalidOperationException("Invalid handle");

        reader.Details.Blobs[type] = new byte[size];
        new Span<byte>(data, size).CopyTo(reader.Details.Blobs[type]);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static unsafe byte ReadMemory(DataStream.memory_reader_t* inst, IntPtr addr, nuint* len, void** ret)
    {
        ReaderFunc* reader = (ReaderFunc*)inst->context;
        Debug.Assert(*len <= int.MaxValue);
        *ret = NativeMemory.Alloc(*len);
        if (!reader->Read(new ForeignPtr((nuint)addr), new Span<byte>((byte*)*ret, (int)*len)))
            return 0;

        return 1;
    }

    [UnmanagedCallersOnly]
#pragma warning disable IDE0060 // Remove unused parameter
    private static unsafe void FreeMemory(DataStream.memory_reader_t* _, nuint _len, void* data)
    {
        NativeMemory.Free(data);
    }
#pragma warning restore IDE0060 // Remove unused parameter

    private ReaderFunc _reader;
    private ForeignPtr _stream;

    private ReaderFunc Reader => _reader;
    private ForeignPtr Stream => _stream;

    internal struct RemoteConfig
    {
        public ForeignPtr StreamStart {get; init;}
        public bool IsLittleEndian {get; init;}
        public int PtrSize {get; init;}
    }

    internal struct ForeignPtr
    {
        public readonly nuint Value;
        public ForeignPtr(nuint rawValue) { Value = rawValue; }
    }

    internal struct ForeignU32
    {
        public readonly uint Value;
        public ForeignU32(uint raw) { Value = raw;}
    }

    private struct ReaderFunc
    {
        private readonly unsafe delegate* unmanaged<ulong, uint, IntPtr, byte*, int> _func;
        private readonly unsafe IntPtr _userData;
        public unsafe ReaderFunc(delegate* unmanaged<ulong, uint, IntPtr, byte*, int> readerFunc, IntPtr userData)
        {
            _func = readerFunc;
            _userData = userData;
        }

        public byte[]? Read(ForeignPtr ptr, uint count)
        {
            byte[] arr = new byte[count];
            if (!Read(ptr, arr))
                return null;
            return arr;
        }

        public bool Read(ForeignPtr ptr, Span<byte> result)
        {
            unsafe
            {
                fixed (byte *dest = result)
                {
                    return _func (ptr.Value, (uint)result.Length, _userData, dest) == 0;
                }
            }
        }

        public ForeignU32 ReadU32(ForeignPtr ptr)
        {
            Span<byte> dest = stackalloc byte[4];
            if (!Read(ptr, dest))
                throw new InvalidOperationException();
            return new ForeignU32(BitConverter.ToUInt32(dest));
        }
    }

}
