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

    [InlineData(true, 8)]
    [InlineData(true, 4)]
    [InlineData(false, 8)]
    [Theory]
    public unsafe void CanReadGoodHeader(bool isLittleEndian, int pointerSize)
    {
        Virtual.VirtualMemorySystem vms = new(isLittleEndian, pointerSize);
        vms.AddNullPage();
        Virtual.VirtualMemorySystem.ExternalPtr ptr = vms.MakeExternalPtrRaw(0x1000u);
        vms.AddRange(Virtual.VirtualDataContext.CreateGoodContext(vms, ptr));

        Assert.True(vms.TryReadUInt32(ptr, out uint magic));
        Assert.Equal(DataContractReader.Magic, magic);

        using var dcrReader = vms.CreateReaderCallback();
        DataContractReader dcr = new();
        dcr.SetReaderFunc(&Virtual.VirtualMemorySystem.Reader, dcrReader.UserData);
        dcr.SetStream((nuint)vms.ToRawValue(ptr));
        Assert.True(dcr.Config.IsLittleEndian == isLittleEndian);
    }
}
