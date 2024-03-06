// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Diagnostics.DataContractReader;

public sealed class DataContractReader : IDisposable
{
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
            Console.WriteLine ("Stream starts at 0x{0:x}", _stream.Value);
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
