// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Diagnostics.DataContractReader
{
    // Names should match those used in runtime to add data to the stream.
    // See src/native/public/cdac/cdac/ds_types.h
    public enum DSType
    {
        Ptr,
        SOSBreakingChangeVersion,
        ThreadStore
    }
}
