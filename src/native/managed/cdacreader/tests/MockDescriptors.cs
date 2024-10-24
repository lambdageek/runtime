// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DataContractReader.Contracts;

namespace Microsoft.Diagnostics.DataContractReader.UnitTests;

public class MockDescriptors
{
    private static readonly (string Name, DataType Type)[] MethodTableFields = new[]
    {
        (nameof(Data.MethodTable.MTFlags), DataType.uint32),
        (nameof(Data.MethodTable.BaseSize), DataType.uint32),
        (nameof(Data.MethodTable.MTFlags2), DataType.uint32),
        (nameof(Data.MethodTable.EEClassOrCanonMT), DataType.nuint),
        (nameof(Data.MethodTable.Module), DataType.pointer),
        (nameof(Data.MethodTable.ParentMethodTable), DataType.pointer),
        (nameof(Data.MethodTable.NumInterfaces), DataType.uint16),
        (nameof(Data.MethodTable.NumVirtuals), DataType.uint16),
        (nameof(Data.MethodTable.PerInstInfo), DataType.pointer),
        (nameof(Data.MethodTable.AuxiliaryData), DataType.pointer),
    };

    private static readonly (string Name, DataType Type)[] EEClassFields = new[]
    {
        (nameof(Data.EEClass.MethodTable), DataType.pointer),
        (nameof(Data.EEClass.CorTypeAttr), DataType.uint32),
        (nameof(Data.EEClass.NumMethods), DataType.uint16),
        (nameof(Data.EEClass.InternalCorElementType), DataType.uint8),
        (nameof(Data.EEClass.NumNonVirtualSlots), DataType.uint16),
    };

    private static readonly (string Name, DataType Type)[] ArrayClassFields = new[]
    {
        (nameof(Data.ArrayClass.Rank), DataType.uint8),
    };

    private static readonly Target.TypeInfo ObjectTypeInfo = new()
    {
        Fields = new Dictionary<string, Target.FieldInfo> {
            { "m_pMethTab", new() { Offset = 0, Type = DataType.pointer} },
        }
    };

    private static readonly Target.TypeInfo StringTypeInfo = new Target.TypeInfo()
    {
        Fields = new Dictionary<string, Target.FieldInfo> {
            { "m_StringLength", new() { Offset = 0x8, Type = DataType.uint32} },
            { "m_FirstChar", new() { Offset = 0xc, Type = DataType.uint16} },
        }
    };

    private static readonly Target.TypeInfo ArrayTypeInfo = new Target.TypeInfo()
    {
        Fields = new Dictionary<string, Target.FieldInfo> {
            { "m_NumComponents", new() { Offset = 0x8, Type = DataType.uint32} },
        },
    };

    private static readonly Target.TypeInfo SyncTableEntryInfo = new Target.TypeInfo()
    {
        Fields = new Dictionary<string, Target.FieldInfo> {
            { nameof(Data.SyncTableEntry.SyncBlock), new() { Offset = 0, Type = DataType.pointer} },
        },
    };

    private static readonly Target.TypeInfo SyncBlockTypeInfo = new Target.TypeInfo()
    {
        Fields = new Dictionary<string, Target.FieldInfo> {
            { nameof(Data.SyncBlock.InteropInfo), new() { Offset = 0, Type = DataType.pointer} },
        },
    };

    private static readonly Target.TypeInfo InteropSyncBlockTypeInfo = new Target.TypeInfo()
    {
        Fields = new Dictionary<string, Target.FieldInfo> {
            { nameof(Data.InteropSyncBlockInfo.RCW), new() { Offset = 0, Type = DataType.pointer} },
            { nameof(Data.InteropSyncBlockInfo.CCW), new() { Offset = 0x8, Type = DataType.pointer} },
        },
    };

    public class RuntimeTypeSystem
    {
        internal const ulong TestFreeObjectMethodTableGlobalAddress = 0x00000000_7a0000a0;
        internal const ulong TestFreeObjectMethodTableAddress = 0x00000000_7a0000a8;

        internal static void AddTypes(TargetTestHelpers targetTestHelpers, Dictionary<DataType, Target.TypeInfo> types)
        {
            var layout = targetTestHelpers.LayoutFields(MethodTableFields);
            types[DataType.MethodTable] = new Target.TypeInfo() { Fields = layout.Fields, Size = layout.Stride };
            var eeClassLayout = targetTestHelpers.LayoutFields(EEClassFields);
            layout = eeClassLayout;
            types[DataType.EEClass] = new Target.TypeInfo() { Fields = layout.Fields, Size = layout.Stride };
            layout = targetTestHelpers.ExtendLayout(ArrayClassFields, eeClassLayout);
            types[DataType.ArrayClass] = new Target.TypeInfo() { Fields = layout.Fields, Size = layout.Stride };
        }

        internal static readonly (string Name, ulong Value, string? Type)[] Globals =
        [
            (nameof(Constants.Globals.FreeObjectMethodTable), TestFreeObjectMethodTableGlobalAddress, null),
            (nameof(Constants.Globals.MethodDescAlignment), 8, nameof(DataType.uint64)),
        ];

        internal static MockMemorySpace.Builder AddGlobalPointers(Target.TypeInfo methodTableTypeInfo, MockMemorySpace.Builder builder)
        {
            return AddFreeObjectMethodTable(methodTableTypeInfo, builder);
        }

        private static MockMemorySpace.Builder AddFreeObjectMethodTable(Target.TypeInfo methodTableTypeInfo, MockMemorySpace.Builder builder)
        {
            TargetTestHelpers targetTestHelpers = builder.TargetTestHelpers;
            MockMemorySpace.HeapFragment globalAddr = new() { Name = "Address of Free Object Method Table", Address = TestFreeObjectMethodTableGlobalAddress, Data = new byte[targetTestHelpers.PointerSize] };
            targetTestHelpers.WritePointer(globalAddr.Data, TestFreeObjectMethodTableAddress);
            return builder.AddHeapFragments([
                globalAddr,
                new () { Name = "Free Object Method Table", Address = TestFreeObjectMethodTableAddress, Data = new byte[targetTestHelpers.SizeOfTypeInfo(methodTableTypeInfo)] }
            ]);
        }

        internal static MockMemorySpace.Builder AddEEClass(Target.TypeInfo eeClassTypeInfo, MockMemorySpace.Builder builder, TargetPointer eeClassPtr, string name, TargetPointer canonMTPtr, uint attr, ushort numMethods, ushort numNonVirtualSlots)
        {
            TargetTestHelpers targetTestHelpers = builder.TargetTestHelpers;
            MockMemorySpace.HeapFragment eeClassFragment = new() { Name = $"EEClass '{name}'", Address = eeClassPtr, Data = new byte[targetTestHelpers.SizeOfTypeInfo(eeClassTypeInfo)] };
            Span<byte> dest = eeClassFragment.Data;
            targetTestHelpers.WritePointer(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.MethodTable)].Offset), canonMTPtr);
            targetTestHelpers.Write(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.CorTypeAttr)].Offset), attr);
            targetTestHelpers.Write(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.NumMethods)].Offset), numMethods);
            targetTestHelpers.Write(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.NumNonVirtualSlots)].Offset), numNonVirtualSlots);
            return builder.AddHeapFragment(eeClassFragment);
        }

        internal static MockMemorySpace.Builder AddArrayClass(Dictionary<DataType, Target.TypeInfo> types, MockMemorySpace.Builder builder, TargetPointer eeClassPtr, string name, TargetPointer canonMTPtr, uint attr, ushort numMethods, ushort numNonVirtualSlots, byte rank)
        {
            TargetTestHelpers targetTestHelpers = builder.TargetTestHelpers;
            Target.TypeInfo eeClassTypeInfo = types[DataType.EEClass];
            Target.TypeInfo arrayClassTypeInfo = types[DataType.ArrayClass];
            int size = (int)arrayClassTypeInfo.Size.Value;
            MockMemorySpace.HeapFragment eeClassFragment = new() { Name = $"ArrayClass '{name}'", Address = eeClassPtr, Data = new byte[size] };
            Span<byte> dest = eeClassFragment.Data;
            targetTestHelpers.WritePointer(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.MethodTable)].Offset), canonMTPtr);
            targetTestHelpers.Write(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.CorTypeAttr)].Offset), attr);
            targetTestHelpers.Write(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.NumMethods)].Offset), numMethods);
            targetTestHelpers.Write(dest.Slice(eeClassTypeInfo.Fields[nameof(Data.EEClass.NumNonVirtualSlots)].Offset), numNonVirtualSlots);
            targetTestHelpers.Write(dest.Slice(arrayClassTypeInfo.Fields[nameof(Data.ArrayClass.Rank)].Offset), rank);
            return builder.AddHeapFragment(eeClassFragment);
        }

        internal static MockMemorySpace.Builder AddMethodTable(Target.TypeInfo methodTableTypeInfo, MockMemorySpace.Builder builder, TargetPointer methodTablePtr, string name, TargetPointer eeClassOrCanonMT, uint mtflags, uint mtflags2, uint baseSize,
                                                            TargetPointer module, TargetPointer parentMethodTable, ushort numInterfaces, ushort numVirtuals)
        {
            TargetTestHelpers targetTestHelpers = builder.TargetTestHelpers;
            MockMemorySpace.HeapFragment methodTableFragment = new() { Name = $"MethodTable '{name}'", Address = methodTablePtr, Data = new byte[targetTestHelpers.SizeOfTypeInfo(methodTableTypeInfo)] };
            Span<byte> dest = methodTableFragment.Data;
            targetTestHelpers.WritePointer(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.EEClassOrCanonMT)].Offset), eeClassOrCanonMT);
            targetTestHelpers.Write(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.MTFlags)].Offset), mtflags);
            targetTestHelpers.Write(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.MTFlags2)].Offset), mtflags2);
            targetTestHelpers.Write(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.BaseSize)].Offset), baseSize);
            targetTestHelpers.WritePointer(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.Module)].Offset), module);
            targetTestHelpers.WritePointer(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.ParentMethodTable)].Offset), parentMethodTable);
            targetTestHelpers.Write(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.NumInterfaces)].Offset), numInterfaces);
            targetTestHelpers.Write(dest.Slice(methodTableTypeInfo.Fields[nameof(Data.MethodTable.NumVirtuals)].Offset), numVirtuals);

            // TODO fill in the rest of the fields
            return builder.AddHeapFragment(methodTableFragment);
        }
    }

    public static class Object
    {
        private const ulong TestStringMethodTableGlobalAddress = 0x00000000_100000a0;
        private const ulong TestStringMethodTableAddress = 0x00000000_100000a8;
        internal const ulong TestArrayBoundsZeroGlobalAddress = 0x00000000_100000b0;

        private const ulong TestSyncTableEntriesGlobalAddress = 0x00000000_100000c0;
        private const ulong TestSyncTableEntriesAddress = 0x00000000_f0000000;

        internal const ulong TestObjectToMethodTableUnmask = 0x7;
        internal const ulong TestSyncBlockValueToObjectOffset = sizeof(uint);

        internal static Dictionary<DataType, Target.TypeInfo> Types(TargetTestHelpers helpers)
        {
            Dictionary<DataType, Target.TypeInfo> types = new();
            RuntimeTypeSystem.AddTypes(helpers, types);
            types[DataType.Object] = ObjectTypeInfo;
            types[DataType.String] = StringTypeInfo;
            types[DataType.Array] = ArrayTypeInfo with { Size = helpers.ArrayBaseSize };
            types[DataType.SyncTableEntry] = SyncTableEntryInfo with { Size = (uint)helpers.SizeOfTypeInfo(SyncTableEntryInfo) };
            types[DataType.SyncBlock] = SyncBlockTypeInfo;
            types[DataType.InteropSyncBlockInfo] = InteropSyncBlockTypeInfo;
            return types;
        }

        internal static (string Name, ulong Value, string? Type)[] Globals(TargetTestHelpers helpers) => RuntimeTypeSystem.Globals.Concat(
        [
            (nameof(Constants.Globals.ObjectToMethodTableUnmask), TestObjectToMethodTableUnmask, "uint8"),
            (nameof(Constants.Globals.StringMethodTable), TestStringMethodTableGlobalAddress, null),
            (nameof(Constants.Globals.ArrayBoundsZero), TestArrayBoundsZeroGlobalAddress, null),
            (nameof(Constants.Globals.SyncTableEntries), TestSyncTableEntriesGlobalAddress, null),
            (nameof(Constants.Globals.ObjectHeaderSize), helpers.ObjHeaderSize, "uint32"),
            (nameof(Constants.Globals.SyncBlockValueToObjectOffset), TestSyncBlockValueToObjectOffset, "uint16"),
        ]).ToArray();

        internal static MockMemorySpace.Builder AddGlobalPointers(Target.TypeInfo methodTableTypeInfo, MockMemorySpace.Builder builder)
        {
            builder = RuntimeTypeSystem.AddGlobalPointers(methodTableTypeInfo, builder);
            builder = AddStringMethodTablePointer(builder);
            builder = AddSyncTableEntriesPointer(builder);
            return builder;
        }

        private static MockMemorySpace.Builder AddStringMethodTablePointer(MockMemorySpace.Builder builder)
        {
            TargetTestHelpers targetTestHelpers = builder.TargetTestHelpers;
            MockMemorySpace.HeapFragment fragment = new() { Name = "Address of String Method Table", Address = TestStringMethodTableGlobalAddress, Data = new byte[targetTestHelpers.PointerSize] };
            targetTestHelpers.WritePointer(fragment.Data, TestStringMethodTableAddress);
            return builder.AddHeapFragments([
                fragment,
                new () { Name = "String Method Table", Address = TestStringMethodTableAddress, Data = new byte[targetTestHelpers.PointerSize] }
            ]);
        }

        private static MockMemorySpace.Builder AddSyncTableEntriesPointer(MockMemorySpace.Builder builder)
        {
            TargetTestHelpers targetTestHelpers = builder.TargetTestHelpers;
            MockMemorySpace.HeapFragment fragment = new() { Name = "Address of Sync Table Entries", Address = TestSyncTableEntriesGlobalAddress, Data = new byte[targetTestHelpers.PointerSize] };
            targetTestHelpers.WritePointer(fragment.Data, TestSyncTableEntriesAddress);
            return builder.AddHeapFragment(fragment);
        }

        internal static MockMemorySpace.Builder AddObject(TargetTestHelpers targetTestHelpers, MockMemorySpace.Builder builder, TargetPointer address, TargetPointer methodTable)
        {
            MockMemorySpace.HeapFragment fragment = new() { Name = $"Object : MT = '{methodTable}'", Address = address, Data = new byte[targetTestHelpers.SizeOfTypeInfo(ObjectTypeInfo)] };
            Span<byte> dest = fragment.Data;
            targetTestHelpers.WritePointer(dest.Slice(ObjectTypeInfo.Fields["m_pMethTab"].Offset), methodTable);
            return builder.AddHeapFragment(fragment);
        }

        internal static MockMemorySpace.Builder AddObjectWithSyncBlock(TargetTestHelpers targetTestHelpers, MockMemorySpace.Builder builder, TargetPointer address, TargetPointer methodTable, uint syncBlockIndex, TargetPointer rcw, TargetPointer ccw)
        {
            const uint IsSyncBlockIndexBits = 0x08000000;
            const uint SyncBlockIndexMask = (1 << 26) - 1;
            if ((syncBlockIndex & SyncBlockIndexMask) != syncBlockIndex)
                throw new ArgumentOutOfRangeException(nameof(syncBlockIndex), "Invalid sync block index");

            builder = AddObject(targetTestHelpers, builder, address, methodTable);

            // Add the sync table value before the object
            uint syncTableValue = IsSyncBlockIndexBits | syncBlockIndex;
            TargetPointer syncTableValueAddr = address - TestSyncBlockValueToObjectOffset;
            MockMemorySpace.HeapFragment fragment = new() { Name = $"Sync Table Value : index = {syncBlockIndex}", Address = syncTableValueAddr, Data = new byte[sizeof(uint)] };
            targetTestHelpers.Write(fragment.Data, syncTableValue);
            builder = builder.AddHeapFragment(fragment);

            // Add the actual sync block and associated data
            return AddSyncBlock(targetTestHelpers, builder, syncBlockIndex, rcw, ccw);
        }

        private static MockMemorySpace.Builder AddSyncBlock(TargetTestHelpers targetTestHelpers, MockMemorySpace.Builder builder, uint index, TargetPointer rcw, TargetPointer ccw)
        {
            // Tests write the sync blocks starting at TestSyncBlocksAddress
            const ulong TestSyncBlocksAddress = 0x00000000_e0000000;
            int syncBlockSize = targetTestHelpers.SizeOfTypeInfo(SyncBlockTypeInfo);
            int interopSyncBlockInfoSize = targetTestHelpers.SizeOfTypeInfo(InteropSyncBlockTypeInfo);
            ulong syncBlockAddr = TestSyncBlocksAddress + index * (ulong)(syncBlockSize + interopSyncBlockInfoSize);

            // Add the sync table entry - pointing at the sync block
            uint syncTableEntrySize = (uint)targetTestHelpers.SizeOfTypeInfo(SyncTableEntryInfo);
            ulong syncTableEntryAddr = TestSyncTableEntriesAddress + index * syncTableEntrySize;
            MockMemorySpace.HeapFragment syncTableEntry = new() { Name = $"SyncTableEntries[{index}]", Address = syncTableEntryAddr, Data = new byte[syncTableEntrySize] };
            Span<byte> syncTableEntryData = syncTableEntry.Data;
            targetTestHelpers.WritePointer(syncTableEntryData.Slice(SyncTableEntryInfo.Fields[nameof(Data.SyncTableEntry.SyncBlock)].Offset), syncBlockAddr);

            // Add the sync block - pointing at the interop sync block info
            ulong interopInfoAddr = syncBlockAddr + (ulong)syncBlockSize;
            MockMemorySpace.HeapFragment syncBlock = new() { Name = $"Sync Block", Address = syncBlockAddr, Data = new byte[syncBlockSize] };
            Span<byte> syncBlockData = syncBlock.Data;
            targetTestHelpers.WritePointer(syncBlockData.Slice(SyncBlockTypeInfo.Fields[nameof(Data.SyncBlock.InteropInfo)].Offset), interopInfoAddr);

            // Add the interop sync block info
            MockMemorySpace.HeapFragment interopInfo = new() { Name = $"Interop Sync Block Info", Address = interopInfoAddr, Data = new byte[interopSyncBlockInfoSize] };
            Span<byte> interopInfoData = interopInfo.Data;
            targetTestHelpers.WritePointer(interopInfoData.Slice(InteropSyncBlockTypeInfo.Fields[nameof(Data.InteropSyncBlockInfo.RCW)].Offset), rcw);
            targetTestHelpers.WritePointer(interopInfoData.Slice(InteropSyncBlockTypeInfo.Fields[nameof(Data.InteropSyncBlockInfo.CCW)].Offset), ccw);

            return builder.AddHeapFragments([syncTableEntry, syncBlock, interopInfo]);
        }

        internal static MockMemorySpace.Builder AddStringObject(TargetTestHelpers targetTestHelpers, MockMemorySpace.Builder builder, TargetPointer address, string value)
        {
            int size = targetTestHelpers.SizeOfTypeInfo(ObjectTypeInfo) + targetTestHelpers.SizeOfTypeInfo(StringTypeInfo) + value.Length * sizeof(char);
            MockMemorySpace.HeapFragment fragment = new() { Name = $"String = '{value}'", Address = address, Data = new byte[size] };
            Span<byte> dest = fragment.Data;
            targetTestHelpers.WritePointer(dest.Slice(ObjectTypeInfo.Fields["m_pMethTab"].Offset), TestStringMethodTableAddress);
            targetTestHelpers.Write(dest.Slice(StringTypeInfo.Fields["m_StringLength"].Offset), (uint)value.Length);
            MemoryMarshal.Cast<char, byte>(value).CopyTo(dest.Slice(StringTypeInfo.Fields["m_FirstChar"].Offset));
            return builder.AddHeapFragment(fragment);
        }

        internal static MockMemorySpace.Builder AddArrayObject(MockMemorySpace.Builder builder, TargetPointer address, Array array)
        {
            TargetTestHelpers targetTestHelpers = builder.TargetTestHelpers;
            bool isSingleDimensionZeroLowerBound = array.Rank == 1 && array.GetLowerBound(0) == 0;

            // Bounds are part of the array object for non-single dimension or non-zero lower bound arrays
            //   << fields that are part of the array type info >>
            //   int32_t bounds[rank];
            //   int32_t lowerBounds[rank];
            int size = targetTestHelpers.SizeOfTypeInfo(ObjectTypeInfo) + targetTestHelpers.SizeOfTypeInfo(ArrayTypeInfo);
            if (!isSingleDimensionZeroLowerBound)
                size += array.Rank * sizeof(int) * 2;

            ulong methodTableAddress = (address.Value + (ulong)size + (TestObjectToMethodTableUnmask - 1)) & ~(TestObjectToMethodTableUnmask - 1);
            Dictionary<DataType, Target.TypeInfo> types = Types(targetTestHelpers); // TODO(cdac): pass in types
            ulong arrayClassAddress = methodTableAddress + (ulong)targetTestHelpers.SizeOfTypeInfo(types[DataType.MethodTable]);

            uint flags = (uint)(RuntimeTypeSystem_1.WFLAGS_HIGH.HasComponentSize | RuntimeTypeSystem_1.WFLAGS_HIGH.Category_Array) | (uint)array.Length;
            if (isSingleDimensionZeroLowerBound)
                flags |= (uint)RuntimeTypeSystem_1.WFLAGS_HIGH.Category_IfArrayThenSzArray;

            string name = string.Join(',', array);

            builder = RuntimeTypeSystem.AddArrayClass(types, builder, arrayClassAddress, name, methodTableAddress,
                attr: 0, numMethods: 0, numNonVirtualSlots: 0, rank: (byte)array.Rank);
            builder = RuntimeTypeSystem.AddMethodTable(types[DataType.MethodTable], builder, methodTableAddress, name, arrayClassAddress,
                mtflags: flags, mtflags2: default, baseSize: targetTestHelpers.ArrayBaseBaseSize,
                module: TargetPointer.Null, parentMethodTable: TargetPointer.Null, numInterfaces: 0, numVirtuals: 0);

            MockMemorySpace.HeapFragment fragment = new() { Name = $"Array = '{string.Join(',', array)}'", Address = address, Data = new byte[size] };
            Span<byte> dest = fragment.Data;
            targetTestHelpers.WritePointer(dest.Slice(ObjectTypeInfo.Fields["m_pMethTab"].Offset), methodTableAddress);
            targetTestHelpers.Write(dest.Slice(ArrayTypeInfo.Fields["m_NumComponents"].Offset), (uint)array.Length);
            return builder.AddHeapFragment(fragment);
        }
    }
}
