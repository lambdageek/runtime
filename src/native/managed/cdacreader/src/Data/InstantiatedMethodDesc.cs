// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.DataContractReader.Data;

internal sealed class InstantiatedMethodDesc : IData<InstantiatedMethodDesc>
{
    static InstantiatedMethodDesc IData<InstantiatedMethodDesc>.Create(Target target, TargetPointer address) => new InstantiatedMethodDesc(target, address);
    public InstantiatedMethodDesc(Target target, TargetPointer address)
    {
        Target.TypeInfo type = target.GetTypeInfo(DataType.InstantiatedMethodDesc);

        Flags2 = target.Read<ushort>(address + (ulong)type.Fields[nameof(Flags2)].Offset);
        PerInstInfo = target.ReadPointer(address + (ulong)type.Fields[nameof(PerInstInfo)].Offset);
    }

    public ushort Flags2 { get; init; }
    public TargetPointer PerInstInfo { get; init; }
}
