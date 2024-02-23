// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader;

internal static class Entrypoints
{
    private const string CDAC = "cdac_reader_";

    private enum Result : int {
        Ok = 0,
        EFail = -1,
    }

    private static DataContractReader? Unwrap(IntPtr handle)
    {
        GCHandle h = GCHandle.FromIntPtr(handle);
        return h.Target as DataContractReader;
    }

    [UnmanagedCallersOnly(EntryPoint=CDAC+"init")]
    private static unsafe Result Init(IntPtr *handleOut)
    {
        try
        {
            DataContractReader reader = new();
            GCHandle handle = GCHandle.Alloc(reader);
            *handleOut = GCHandle.ToIntPtr(handle);
            return Result.Ok;
        }
        catch (Exception)
        {
            return Result.EFail;
        }
    }

    [UnmanagedCallersOnly(EntryPoint=CDAC+"destroy")]
    private static void Destroy(IntPtr handle)
    {
        try
        {
            GCHandle h = GCHandle.FromIntPtr(handle);
            DataContractReader? reader = h.Target as DataContractReader;
            h.Free();
            reader?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    [UnmanagedCallersOnly(EntryPoint=CDAC+"set_reader_func")]
    private static unsafe Result SetReaderFunc(IntPtr handle, delegate* unmanaged<ulong, uint, IntPtr, byte*, int/*FIXME: Result*/> readerFunc, IntPtr userData)
    {
        try
        {
            DataContractReader? reader = Unwrap(handle);
            reader?.SetReaderFunc(readerFunc, userData);
            return Result.Ok;
        } catch (Exception)
        {
            return Result.EFail;
        }
    }
}
