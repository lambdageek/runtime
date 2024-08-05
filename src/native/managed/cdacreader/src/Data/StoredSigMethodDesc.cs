// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.DataContractReader.Data;

internal sealed class StoredSigMethodDesc : IData<StoredSigMethodDesc>
{
    static StoredSigMethodDesc IData<StoredSigMethodDesc>.Create(Target target, TargetPointer address) => new StoredSigMethodDesc(target, address);
    public StoredSigMethodDesc(Target target, TargetPointer address)
    {
        Target.TypeInfo type = target.GetTypeInfo(DataType.StoredSigMethodDesc);

        ExtendedFlags = target.Read<uint>(address + (ulong)type.Fields[nameof(ExtendedFlags)].Offset);
    }

    public uint ExtendedFlags { get; init; }
}
