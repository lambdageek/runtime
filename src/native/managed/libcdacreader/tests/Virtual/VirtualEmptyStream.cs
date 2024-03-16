// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Microsoft.DotNet.Diagnostics.DataContractReader.Tests.Virtual;

public class VirtualEmptyStream : VirtualBufferBackedStream, IVirtualMemoryRangeOwner
{
    private VirtualEmptyStream(VirtualMemorySystem virtualMemory, KnownStream id, BufferBackedRange buffer) : base(virtualMemory, id, buffer)
    {
    }

    public class EmptyStreamBuilder : Builder<VirtualEmptyStream>
    {
        public EmptyStreamBuilder(VirtualMemorySystem virtualMemory, KnownStream id) : base(virtualMemory, id) { }
        protected override VirtualEmptyStream CreateInstance(BufferBackedRange buf) => new VirtualEmptyStream(VirtualMemory, _id, buf);

        // potentially need to override InitDataBlockContent to make pos point to some real memory and curr/end to point after it.
        // not sure if it's ok for the data block to be completely empty.

    }


}

