// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace Mono;

public static partial class Control
{

    public static class Delimited
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
        // FIXME - we should wrap "body" in a try/catch - we don't want exceptions to skip the
        // delimit.restore opcode. (or we need the delimit.restore opcode in the interpreter to do a
        // reasonable thing when it's in a "finally"
        public static R Delimit<R>(Func<R> body) => body (); // IMPORTANT: do not change this - the interpreter looks for a call to a delegate to set up the continuation delimiter

        /// Captures the current continuation up to the nearest dynamically enclosing
        /// Delimit and calls continuationConsumer passing to it a handle the the captured
        /// continuation.  The continuation consumer executes as if it is the body of
        /// Delimit and returns an answer to it.
        /// The continuation consumer must not return normally, it must invoke some continuation.
        public static T? TransferControl<T> (Action<ContinuationHandle<T>> continuationConsumer) => (T?)TransferControl_Internal((contHandle) => continuationConsumer (new ContinuationHandle<T> { Value = contHandle }));


        [Intrinsic]
        [DynamicDependency("ExecControlDelegateA`1")] // to call the continuation consumer
        private static object? TransferControl_Internal (Action<IntPtr> continuationConsumerWrapper) => TransferControl_Internal (continuationConsumerWrapper);

        /// Given a continuation handle and an answer to give to the continuation, resumes
        /// the continuation by placing it back as the active stack. The rest of the current computation following ResumeContinuation is abandoned.
        [DoesNotReturn]
        public static void ResumeContinuation<T> (ContinuationHandle<T> continuation, T? answer) => ResumeContinuation_Internal(continuation.Value, (object?)answer);

        [DoesNotReturn]
        [Intrinsic]
        private static void ResumeContinuation_Internal (IntPtr continuation, object? answer) => ResumeContinuation_Internal(continuation, answer);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static void ExecControlDelegateA<T>(Action<T> d, T x) => d (x);

    }

}
