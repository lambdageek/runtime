// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace Mono;

public static partial class DelimitedContinuations
{

    // Represents a captured continuation from a top frame accepting a value TCont.
    // If continuations were first class objects, this would just be Action<TCont>.
    // The continuations are one-shot, so they can only be resumed once.
    // FIXME: this is <forall T>(T=>R) delimited continuations. We at least also need
    //  void-typed.  Possibly we should just add IntPtr versions and build the rest on top.
    //
    public struct ContinuationHandle<TCont>
    {
        private IntPtr _value; // this is a pointer into some saved continuation table in the runtime
        public IntPtr Value { get => _value; init { _value = value; } }

        /// Given an answer to give to the continuation, resumes the continuation by placing it back
        /// as the active stack. The rest of the current computation following the call to Resume is
        /// abandoned.
        [DoesNotReturn]
        public void Resume (TCont? answer) => ResumeContinuation_Internal (Value, (object?)answer);
    }

    public static bool IsSupported {
#if FEATURE_DELIMIT_CONTROL
        get => true;
#else
        get => false;
#endif

    }

    /// Establishes the limit for calls to TransferControl within the given body.
    /// If the body returns
    [Intrinsic]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static R Delimit<R>(Func<R> body)
    {
        // FIXME - we should wrap "body" in a try/catch - we don't want exceptions to skip the
        // delimit.restore opcode. (or we need the delimit.restore opcode in the interpreter to do a
        // reasonable thing when it's in a "finally"
        return body (); // IMPORTANT: do not change this - the interpreter looks for a call to a delegate to set up the continuation delimiter
    }

    /// Captures the current continuation up to the nearest dynamically enclosing
    /// Delimit and calls continuationConsumer passing to it a handle the the captured
    /// continuation.  The continuation consumer executes as if it is the body of
    /// Delimit and returns an answer to it.
    /// The continuation consumer must not return normally, it must invoke some continuation.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T? TransferControl<T> (Action<ContinuationHandle<T>> continuationConsumer)
    {

        object? answer = null;
        IntPtr continuation = CaptureContinuation(ref answer);
        if (continuation != IntPtr.Zero) {
            // this runs on a fresh stack and must not return to TransferControl
             // FIXME: can we access these variables? we will probably need to duplicate part of the old data stack
            continuationConsumer (new ContinuationHandle<T> {Value = continuation });
            Environment.FailFast ("TransferControl<T> continuation consumer must not return!");
            throw null;
        } else {
            // this runs in the original captured stack, and CaptureContinuation writes to 'answer'
            // the value from ContinuationHandle.Resume()
            return (T?)answer;
        }
    }

    [Intrinsic]
    private static IntPtr CaptureContinuation (ref object? answer) => CaptureContinuation(ref answer);


    [DoesNotReturn]
    [Intrinsic]
    private static void ResumeContinuation_Internal (IntPtr continuation, object? answer) => ResumeContinuation_Internal(continuation, answer);

}
