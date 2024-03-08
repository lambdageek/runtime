// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

using Xunit;

namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests;

public class DatacContractReaderTests
{
    [Fact]
    public void TestInstantiate()
    {
        DataContractReader dcr = new();
        Assert.NotNull(dcr);
    }
}
