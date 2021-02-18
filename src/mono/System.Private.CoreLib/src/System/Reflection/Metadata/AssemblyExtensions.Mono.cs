// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace System.Reflection.Metadata
{
    public static partial class AssemblyExtensions
    {
        [CLSCompliant(false)]
        public static unsafe bool TryGetRawMetadata(this Assembly assembly, out byte* blob, out int length) => throw new NotImplementedException();

        internal static void ApplyUpdateInternal(RuntimeAssembly runtimeAssembly, ReadOnlySpan<byte> metadataDelta, ReadOnlySpan<byte> ilDelta, ReadOnlySpan<byte> pdbDelta = default)
        {
#if !FEATURE_METADATA_UPDATE
            throw new NotSupportedException ("Method body replacement not supported in this runtime");
#else
            // System.Private.CoreLib is not editable
            if (runtimeAssembly == typeof(AssemblyExtensions).Assembly)
                throw new InvalidOperationException ("The assembly can not be edited or changed.");

            unsafe
            {
                IntPtr monoAssembly = runtimeAssembly.GetUnderlyingNativeHandle ();
                fixed (byte* metadataDeltaPtr = metadataDelta, ilDeltaPtr = ilDelta, pdbDeltaPtr = pdbDelta)
                {
                    ApplyUpdate_internal(monoAssembly, metadataDeltaPtr, metadataDelta.Length, ilDeltaPtr, ilDelta.Length, pdbDeltaPtr, pdbDelta.Length);
                }
            }
#endif
        }

        internal static void ApplyUpdateSdb(Assembly assembly, byte[] metadataDelta, byte[] ilDelta, byte[]? pdbDelta = null)
        {
            ReadOnlySpan<byte> md = metadataDelta;
            ReadOnlySpan<byte> il = ilDelta;
            ReadOnlySpan<byte> dpdb = pdbDelta == null ? default : pdbDelta;
            ApplyUpdate (assembly, md, il, dpdb);
        }

#if FEATURE_METADATA_UPDATE
        [MethodImpl (MethodImplOptions.InternalCall)]
        private static unsafe extern void ApplyUpdate_internal (IntPtr base_assm, byte* dmeta_bytes, int dmeta_length, byte *dil_bytes, int dil_length, byte *dpdb_bytes, int dpdb_length);
#endif

    }
}
