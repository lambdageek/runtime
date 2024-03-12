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
        var streams = new Virtual.VirtualAbstractStream[3]{
            new Virtual.VirtualAbstractStream.MissingStream(vms, (ushort)Virtual.VirtualAbstractStream.KnownStreams.Types),
            new Virtual.VirtualAbstractStream.MissingStream(vms, (ushort)Virtual.VirtualAbstractStream.KnownStreams.Blobs),
            new Virtual.VirtualAbstractStream.MissingStream(vms, (ushort)Virtual.VirtualAbstractStream.KnownStreams.Instances)
        };
        Virtual.VirtualMemorySystem.ExternalPtr headerPtr = vms.NullPointer;
        Virtual.VirtualDataContext.CreateGoodContext(vms, streams, (headerStart) => headerPtr = headerStart);
        vms.Reservations.Complete();

        Assert.True(vms.TryReadUInt32(headerPtr, out uint magic));
        Assert.Equal(DataContractReader.Magic, magic);

        using var dcrReader = vms.CreateReaderCallback();
        using DataContractReader dcr = new();
        dcr.SetReaderFunc(&Virtual.VirtualMemorySystem.Reader, dcrReader.UserData);
        dcr.SetStream((nuint)vms.ToRawValue(headerPtr));
        Assert.True(dcr.Config.IsLittleEndian == isLittleEndian);
    }
}
