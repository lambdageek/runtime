// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.DataContractReader.Contracts;

internal struct Loader_1 : ILoader
{
    internal Loader_1(Target target)
    {
        Target = target;
    }
    private Target Target { get; }

    public ModuleHandle GetModuleHandle(TargetPointer targetPointer)
    {
        return new ModuleHandle(targetPointer);
    }
}
