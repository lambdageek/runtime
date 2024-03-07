// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Diagnostics.DataContractReader;

public sealed class DataContractReader : IDisposable
{
    public const int Magic = 0x646e6300;
    public static readonly ReadOnlyMemory<byte> MagicBE = new byte[] {0x64, 0x6e, 0x63, 0x00};
    public static readonly ReadOnlyMemory<byte> MagicLE = new byte[] {0x00, 0x63, 0x6e, 0x64};
    public DataContractReader()
    {
        Console.Error.WriteLine("DataContractReader created!");
    }

    public void Dispose()
    {
        _reader = default;
    }

    internal unsafe void SetReaderFunc(delegate* unmanaged<ulong, uint, IntPtr, byte*, int> readerFunc, IntPtr userData)
    {
        _reader = new ReaderFunc(readerFunc, userData);
    }

    internal void SetStream(ulong data_stream)
    {
        _stream = new ForeignPtr(data_stream);
        Console.WriteLine ("Stream starts at 0x{0:x}", Stream.Value);
        // TODO: move to a ValidateContext() function
        Span<byte> magic = stackalloc byte[4];
        if (!Reader.Read(Stream, magic)) {
            throw new Exception("couldn't read magic");
        }
        bool isLittleEndian;
        if (magic.SequenceEqual(MagicLE.Span))
        {
            isLittleEndian = true;
        } else if (magic.SequenceEqual(MagicBE.Span))
        {
            isLittleEndian = false;
        } else {
            Console.WriteLine ("Expected magic, got 0x{0:x} 0x{1:x} 0x{2:x} 0x{3:x}", magic[0], magic[1], magic[2], magic[3]);
            throw new Exception ("incorrect magic value");
        }
        Console.WriteLine ("target is {0}", isLittleEndian ? "LE" : "BE");
    }

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
        public readonly ulong Value;
        public ForeignPtr(ulong rawValue) { Value = rawValue; }
    }

    internal struct ForeignU32
    {
        private readonly uint Value;
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
