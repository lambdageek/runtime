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

    private (Virtual.VirtualMemorySystem virtualMemory, Virtual.VirtualMemorySystem.ExternalPtr dataContextPtr) CreateBaselineVirtualMemory(bool isLittleEndian, int pointerSize)
    {
        Virtual.VirtualMemorySystem vms = new(isLittleEndian, pointerSize);
        vms.AddNullPage();
        var typeDetailsSOSBreakingChangeVersion = new Virtual.VirtualTypeStream.TypeDetails { Id = 2, Version = 1, Name = "SOSBreakingChangeVersion" };
        var typesStream = new Virtual.VirtualTypeStream.TypeStreamBuilder(vms, [
            new Virtual.VirtualTypeStream.TypeEntity {
                Details = new Virtual.VirtualTypeStream.TypeDetails {Id = 1, Version = 1, Name = "Ptr"},
                TotalSize = (uint)pointerSize,
                FieldOffsets = Array.Empty<Virtual.VirtualTypeStream.FieldOffset>()
                },
            new Virtual.VirtualTypeStream.TypeEntity {
                Details = typeDetailsSOSBreakingChangeVersion,
                TotalSize = 4,
                FieldOffsets = Array.Empty<Virtual.VirtualTypeStream.FieldOffset>()
                },
        ]);
        var blobStream = new Virtual.VirtualBlobStream.BlobStreamBuilder(vms, [
            new Virtual.VirtualBlobStream.BlobEntity {
                Type = typeDetailsSOSBreakingChangeVersion,
                Size = 4,
                Data = new byte[] { 0x00, 0x01, 0x02, 0x03 }
            }
        ]);
        var streams = new Virtual.VirtualAbstractStream.Builder[3]{
            typesStream,
            blobStream,
            new Virtual.VirtualEmptyStream.EmptyStreamBuilder(vms, Virtual.VirtualAbstractStream.KnownStream.Instances)
        };
        Virtual.VirtualMemorySystem.ExternalPtr headerPtr = vms.NullPointer;
        Virtual.VirtualDataContext.CreateGoodContext(vms, streams, (headerStart) => headerPtr = headerStart);
        vms.Reservations.Complete();
        return (vms, headerPtr);
    }

    [InlineData(true, 8)]
    [InlineData(true, 4)]
    [InlineData(false, 8)]
    [Theory]
    public unsafe void CanReadGoodHeader(bool isLittleEndian, int pointerSize)
    {
        (Virtual.VirtualMemorySystem vms, Virtual.VirtualMemorySystem.ExternalPtr headerPtr) = CreateBaselineVirtualMemory(isLittleEndian, pointerSize);
        Assert.True(vms.TryReadUInt32(headerPtr, out uint magic));
        Assert.Equal(DataContractReader.Magic, magic);

        using var dcrReader = vms.CreateReaderCallback();
        using DataContractReader dcr = new();
        dcr.SetReaderFunc(&Virtual.VirtualMemorySystem.Reader, dcrReader.UserData);
        dcr.SetStream((nuint)vms.ToRawValue(headerPtr));
        Assert.True(dcr.Config.IsLittleEndian == isLittleEndian);
        var ptrDetails = dcr.Details.TypeDetailsByLocalId[DSType.Ptr];
        Assert.Equal("Ptr", ptrDetails.Name);
        Assert.Equal((nuint)pointerSize, ptrDetails.Size);
    }
}
