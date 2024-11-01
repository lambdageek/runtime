// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DataContractReader.Contracts;
using Xunit;

namespace Microsoft.Diagnostics.DataContractReader.UnitTests;

/// <summary>
/// These tests for the RuntimeTypesystem_1 contract are set up not to use MethodValidation and TypeValidation
/// </summary>
public class MethodDescNoValidationTests
{
    internal class MDNVTarget : TestPlaceholderTarget
    {
        public MDNVTarget(MockTarget.Architecture arch, Dictionary<DataType, Target.TypeInfo> types, ReadFromTargetDelegate reader) : base(arch)
        {
            SetDataCache(new DefaultDataCache(this));
            SetTypeInfoCache(types);
            SetDataReader(reader);
            SetContracts(new TestRegistry () {
                RuntimeTypeSystemContract = new Lazy<IRuntimeTypeSystem>(() => {
                    return new RuntimeTypeSystem_1(this, null, new DisableMethodValidation(this), TargetPointer.Null, 8);
                }),
            });
        }

        public override TargetPointer ReadGlobalPointer(string global)
        {
            // FIXME: get globals from the builder
            return base.ReadGlobalPointer(global);
        }

        public override T ReadGlobal<T>(string global)
        {
            if (global == Constants.Globals.MethodDescTokenRemainderBitCount)
            {
                return (T)(object)(byte)12;
            }
            if (global == Constants.Globals.MethodDescAlignment)
            {
                if (PointerSize == 4)
                    return (T)(object)(ulong)4;
                return (T)(object)(ulong)8;
            }
            return base.ReadGlobal<T>(global);
        }
    }

    internal class DisableMethodValidation : RuntimeTypeSystemHelpers.MethodValidation
    {
        public DisableMethodValidation(Target target) : base(target, 12/*FIXME*/)
        {
        }
        internal override bool ValidateMethodDescPointer(TargetPointer methodDescPointer, out TargetPointer methodDescChunkPointer)
        {
            Data.MethodDesc umd = _target.ProcessedData.GetOrAdd<Data.MethodDesc>(methodDescPointer);
            methodDescChunkPointer = MethodTableQueries.GetMethodDescChunkPointerThrowing(methodDescPointer, umd);
            return true;
        }
    }

    private static void MethodDescHelper(MockTarget.Architecture arch, Action<MockDescriptors.MethodDescriptors> configure, Action<Target> testCase)
    {
        TargetTestHelpers targetTestHelpers = new(arch);

        MockMemorySpace.Builder builder = new(targetTestHelpers);
        MockDescriptors.RuntimeTypeSystem rtsBuilder = new(builder) {
            // arbtrary address range
            TypeSystemAllocator = builder.CreateAllocator(start: 0x00000000_4a000000, end: 0x00000000_4b000000),
        };

        var loaderBuilder = new MockDescriptors.Loader(builder);

        MockDescriptors.Object objectBuilder = new(rtsBuilder) {
            // arbtrary adress range
            ManagedObjectAllocator = builder.CreateAllocator(start: 0x00000000_10000000, end: 0x00000000_20000000),
        };
        var methodDescChunkAllocator = builder.CreateAllocator(start: 0x00000000_20002000, end: 0x00000000_20003000);
        var methodDescBuilder = new MockDescriptors.MethodDescriptors(rtsBuilder, loaderBuilder)
        {
            MethodDescChunkAllocator = methodDescChunkAllocator,
        };

        builder = builder
            .SetContracts([ nameof (Contracts.Object), nameof (Contracts.RuntimeTypeSystem), nameof (Contracts.Loader) ])
            .SetGlobals(MockDescriptors.MethodDescriptors.Globals(targetTestHelpers))
            .SetTypes(methodDescBuilder.Types);

        methodDescBuilder.AddGlobalPointers();

        configure?.Invoke(methodDescBuilder);

        builder.MarkCreated();
        // TODO: get globals from the builder
        var target = new MDNVTarget(arch, rtsBuilder.Types, builder.GetReadContext().ReadFromTarget);
        testCase(target);
    }


    [Theory]
    [ClassData(typeof(MockTarget.StdArch))]
    public void SetupRTS(MockTarget.Architecture arch)
    {
        TargetPointer expectedMethodDesc = default;
        MethodDescHelper(arch,
        (builder) =>
        {
            byte count = 4;
            byte methodDescSize = (byte)(builder.Types[DataType.MethodDesc].Size.Value / builder.MethodDescAlignment);
            byte chunkSize = (byte)(count * methodDescSize);

            var mt = builder.RTSBuilder.AddMethodTable("test method table", mtflags: 0, mtflags2: 0, baseSize: 0, module: TargetPointer.Null, parentMethodTable: TargetPointer.Null, numInterfaces: 0, numVirtuals: 4);
            var chunk = builder.AddMethodDescChunk(methodTable: mt, "test method desc chunk", count: count, size: chunkSize, tokenRange: 0x000000);

            byte methodDescNum = 3; // abitrary, less than "count"
            byte methodDescIndex = (byte)(methodDescNum * methodDescSize);
            Span<byte> dest = builder.BorrowMethodDesc(chunk, methodDescIndex);
            builder.SetMethodDesc(dest, methodDescIndex, slotNum: 1, tokenRemainder: 0x000001);
            expectedMethodDesc = builder.GetMethodDescAddress(chunk, methodDescIndex);
        },
        (target) =>
        {
            var rts = target.Contracts.RuntimeTypeSystem;
            Assert.NotNull(rts);

            MethodDescHandle h1 = rts.GetMethodDescHandle(expectedMethodDesc);
            Assert.Equal(expectedMethodDesc, h1.Address);
        });
    }
}
