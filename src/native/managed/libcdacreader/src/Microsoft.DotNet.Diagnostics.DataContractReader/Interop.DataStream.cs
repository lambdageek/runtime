// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader;

internal static unsafe partial class DataStream
{
#if HELPERS_AS_DIRECT_PINVOKE
    private const string DataStreamLibrary = "datastream";
#else
    private const string DataStreamLibrary = "datastreamlib";
#endif

    internal enum ds_validate_t
    {
        dsv_invalid,
        dsv_little_endian,
        dsv_big_endian,
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    internal struct memory_reader_t
    {
        public delegate* unmanaged<memory_reader_t*, IntPtr, nuint*, void**, byte> read_ptr;
        public delegate* unmanaged<memory_reader_t*, nuint, void*, void> free_ptr;

        public void* context;
    }
#pragma warning restore CS0649

    [LibraryImport(DataStreamLibrary)]
    internal static partial ds_validate_t dnds_validate(uint magic);

    [LibraryImport(DataStreamLibrary)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool dnds_enum_blobs(
        void* cxt,
        delegate* unmanaged<ushort, ushort, void*, IntPtr, byte> on_next,
        IntPtr user_data,
        memory_reader_t* reader);
}
