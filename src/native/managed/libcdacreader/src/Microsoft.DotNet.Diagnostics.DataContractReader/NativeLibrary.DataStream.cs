// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Diagnostics.DataContractReader;

internal static unsafe partial class DataStream
{
    private static bool s_ImportResolverSet;
    internal static unsafe void SetDllImportResolver(ReadOnlySpan<byte> ownBaseDir)
    {
        // FIXME: threading
        if (s_ImportResolverSet)
        {
            return;
        }
#if !HELPERS_AS_DIRECT_PINVOKE
        // If we're going to dynamically load the datastream library, we need to pass a full path to
        // the dlopen call on macOS, since we're going to be running in the context of LLDB which is a hardened runtime app.
        // Hardened runtime does not allow loading libraries with relative paths.
        string baseDir = System.Text.Encoding.UTF8.GetString(ownBaseDir);
        NativeLibrary.SetDllImportResolver(typeof(DataStream).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName == DataStreamLibrary)
            {
                return NativeLibrary.Load(System.IO.Path.Join(baseDir, PlatformSharedLibName(DataStreamLibrary)));
            }

            return IntPtr.Zero;
        });
#endif
        s_ImportResolverSet = true;
    }

    private static string PlatformSharedLibName(string libraryName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return libraryName + ".dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "lib" + libraryName + ".dylib";
        }
        else
        {
            return "lib" + libraryName + ".so";
        }
    }
}
