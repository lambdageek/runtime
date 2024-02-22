//
// This header file may be included multiple times so has no guards
//

//
// Usage of this header file is means defining the below macros.
// This header file will undef these macros unconditionally.
//
// Each TYPE_ID should contain a human readable ID that can be
// converting into an enum value or a string and a version number.
// Once a TYPE_ID is defined it can be moved to any position, but
// should not be entirely removed. If the type's semantics change,
// the version number should be incremented.
//

// Always provide macro defaults
#ifndef BEGIN_TYPE_IDS
#define BEGIN_TYPE_IDS()
#endif // !BEGIN_TYPE_IDS
#ifndef END_TYPE_IDS
#define END_TYPE_IDS()
#endif // !END_TYPE_IDS
#ifndef DEFINE_TYPE_ID
#define DEFINE_TYPE_ID(name, v)
#endif // !DEFINE_TYPE_ID
#ifndef DEFINE_TYPE_ID_LAST
#define DEFINE_TYPE_ID_LAST()
#endif // !DEFINE_TYPE_ID_LAST

BEGIN_TYPE_IDS()
    // Reusable types
    DEFINE_TYPE_ID(Ptr, 1)
    /* DEFINE_TYPE_ID(SLink, 1) */
    /* DEFINE_TYPE_ID(Id_4Byte, 1) */
    /* DEFINE_TYPE_ID(Size_4Byte, 1) */
    /* DEFINE_TYPE_ID(RVA_4Byte, 1) */
    /* DEFINE_TYPE_ID(SLinkOffset, 1) */
    /* DEFINE_TYPE_ID(BeginPtr, 1) */
    /* DEFINE_TYPE_ID(EndPtr, 1) */
    /* DEFINE_TYPE_ID(Begin_4Byte, 1) */
    /* DEFINE_TYPE_ID(End_4Byte, 1) */
    /* DEFINE_TYPE_ID(InstructionPointer, 1) */
    /* DEFINE_TYPE_ID(StackPointer, 1) */
    /* DEFINE_TYPE_ID(ReturnAddress, 1) */

    /* // Specific types */
    /* DEFINE_TYPE_ID(RuntimeBaseAddress, 1) */

    /* DEFINE_TYPE_ID(ThreadStore, 1) */
    /* DEFINE_TYPE_ID(ThreadList, 1) */
    /* DEFINE_TYPE_ID(Thread, 1) */
    /* DEFINE_TYPE_ID(ThreadState, 1) */
    /* DEFINE_TYPE_ID(ThreadRemoved, 1) */

    /* DEFINE_TYPE_ID(Frame, 1) */
    /* DEFINE_TYPE_ID(FramePtr, 1) */

    /* DEFINE_TYPE_ID(FrameVTblTypeMap, 1) */

    /* DEFINE_TYPE_ID(InlinedCallFrame, 1) */
    /* DEFINE_TYPE_ID(CallSiteSP, 1) */
    /* DEFINE_TYPE_ID(CallerReturnAddress, 1) */
    /* DEFINE_TYPE_ID(CalleeSavedFP, 1) */

    /* DEFINE_TYPE_ID(ResumableFrame, 1) */
    /* DEFINE_TYPE_ID(RedirectedThreadFrame, 1) */
    /* DEFINE_TYPE_ID(FaultingExceptionFrame, 1) */
    /* DEFINE_TYPE_ID(FuncEvalFrame, 1) */
    /* DEFINE_TYPE_ID(HelperMethodFrame, 1) */
    /* DEFINE_TYPE_ID(HelperMethodFrame_1OBJ, 1) */
    /* DEFINE_TYPE_ID(HelperMethodFrame_2OBJ, 1) */
    /* DEFINE_TYPE_ID(HelperMethodFrame_3OBJ, 1) */
    /* DEFINE_TYPE_ID(HelperMethodFrame_PROTECTOBJ, 1) */
    /* DEFINE_TYPE_ID(MulticastFrame, 1) */
    /* DEFINE_TYPE_ID(ComMethodFrame, 1) */
    /* DEFINE_TYPE_ID(ComPlusMethodFrame, 1) */
    /* DEFINE_TYPE_ID(ComPrestubMethodFrame, 1) */
    /* DEFINE_TYPE_ID(PInvokeCalliFrame, 1) */
    /* DEFINE_TYPE_ID(HijackFrame, 1) */
    /* DEFINE_TYPE_ID(PrestubMethodFrame, 1) */
    /* DEFINE_TYPE_ID(CallCountingHelperFrame, 1) */
    /* DEFINE_TYPE_ID(StubDispatchFrame, 1) */
    /* DEFINE_TYPE_ID(ExternalMethodFrame, 1) */
    /* DEFINE_TYPE_ID(DynamicHelperFrame, 1) */
    /* DEFINE_TYPE_ID(ProtectByRefsFrame, 1) */
    /* DEFINE_TYPE_ID(ProtectValueClassFrame, 1) */
    /* DEFINE_TYPE_ID(DebuggerClassInitMarkFrame, 1) */
    /* DEFINE_TYPE_ID(DebuggerSecurityCodeMarkFrame, 1) */
    /* DEFINE_TYPE_ID(DebuggerExitFrame, 1) */
    /* DEFINE_TYPE_ID(DebuggerU2MCatchHandlerFrame, 1) */
    /* DEFINE_TYPE_ID(ExceptionFilterFrame, 1) */
    /* DEFINE_TYPE_ID(AssumeByrefFromJITStack, 1) */

    /* DEFINE_TYPE_ID(FrameAttributes, 1) */
    /* DEFINE_TYPE_ID(FCallAddr, 1) */

    /* DEFINE_TYPE_ID(RangeSectionMapConfig, 1) */
    /* DEFINE_TYPE_ID(RangeSectionMap, 1) */
    /* DEFINE_TYPE_ID(RangeSectionTopLevel, 1) */
    /* DEFINE_TYPE_ID(RangeSectionFragment, 1) */
    /* DEFINE_TYPE_ID(RangeSection, 1) */
    /* DEFINE_TYPE_ID(RangeSectionPtr, 1) */
    /* DEFINE_TYPE_ID(RangeSectionToDeletePtr, 1) */
    /* DEFINE_TYPE_ID(Range, 1) */
    /* DEFINE_TYPE_ID(RangeSectionFragmentPointer, 1) */
    /* DEFINE_TYPE_ID(RangeSectionFlags, 1) */

    /* DEFINE_TYPE_ID(HeapList, 1) */
    /* DEFINE_TYPE_ID(MapBase, 1) */
    /* DEFINE_TYPE_ID(HeaderMap, 1) */
    /* DEFINE_TYPE_ID(HeapListPtr, 1) */
    /* DEFINE_TYPE_ID(R2RModulePtr, 1) */

    /* DEFINE_TYPE_ID(Module, 1) */
    /* DEFINE_TYPE_ID(ModulePtr, 1) */

    /* DEFINE_TYPE_ID(MethodDesc, 1) */
    /* DEFINE_TYPE_ID(MethodDescPtr, 1) */
    /* DEFINE_TYPE_ID(Flags_2Byte, 1) */
    /* DEFINE_TYPE_ID(ChunkIndex, 1) */
    /* DEFINE_TYPE_ID(MethodIndex, 1) */

    /* DEFINE_TYPE_ID(MethodDescChunk, 1) */
    /* DEFINE_TYPE_ID(MethodDescData, 1) */

    /* DEFINE_TYPE_ID(ImageDataDirectory, 1) */
    /* DEFINE_TYPE_ID(ImageDataDirectoryPtr, 1) */

    /* DEFINE_TYPE_ID(ReadyToRunInfo, 1) */
    /* DEFINE_TYPE_ID(ReadyToRunInfoPtr, 1) */

    /* DEFINE_TYPE_ID(CodeHeader, 1) */
    /* DEFINE_TYPE_ID(RealCodeHeader, 1) */
    /* DEFINE_TYPE_ID(RealCodeHeaderPtr, 1) */
    /* DEFINE_TYPE_ID(RuntimeFunction, 1) */
    /* DEFINE_TYPE_ID(RuntimeFunctionPtr, 1) */

    /* DEFINE_TYPE_ID(LazyMachState, 1) */
    /* DEFINE_TYPE_ID(CapturedInstructionPointer, 1) */
    /* DEFINE_TYPE_ID(CapturedStackPointer, 1) */
    /* DEFINE_TYPE_ID(CalleeSavedRegisters, 1) */
    /* DEFINE_TYPE_ID(CalleeSavedRegistersPointers, 1) */

    // Used to define last entry.
    // Commonly used for creating a "count" enum value.
    DEFINE_TYPE_ID_LAST()
END_TYPE_IDS()

// Always undef macros
#undef BEGIN_TYPE_IDS
#undef END_TYPE_IDS
#undef DEFINE_TYPE_ID
#undef DEFINE_TYPE_ID_LAST
