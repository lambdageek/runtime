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
        // Represents a captured continuation from a top frame accepting a value TCont to a
        // delimited body returning TDelimit.
        // If continuations were first class objects, this would just be Func<TCont, TDelimit>.
        // The continuations are one-shot, so they can only be resumed once.
        // FIXME: this is <forall T>(T=>R) delimited continuations. We at least also need
        //  void-typed.  Possibly we should just add IntPtr versions and build the rest on top.
        //
        public struct ContinuationHandle<TCont, TDelimit>
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
        public static R Delimit<R>(Func<R> body) => body (); // IMPORTANT: do not change this - the interpreter looks for a call to a delegate to set up the continuation delimiter

        /// Captures the current continuation up to the nearest dynamically enclosing
        /// Delimit and calls continuationConsumer passing to it a handle the the captured
        /// continuation.  The continuation consumer executes as if it is the body of
        /// Delimit and returns an answer to it.
        /// The continuation consumer must not return normally, it must invoke some continuation.
        [Intrinsic]
        [DynamicDependency("ExecControlDelegateA`1")] // to call the continuation consumer
        public static T TransferControl<T, R> (Action<ContinuationHandle<T, R>> continuationConsumer) => TransferControl(continuationConsumer);


        /// Given a continuation handle and an answer to give to the continuation, resumes
        /// the continuation by placing it back as the active stack. The rest of the current computation following ResumeContinuation is abandoned.
        [DoesNotReturn]
        [Intrinsic]
        public static void ResumeContinuation<T, R> (ContinuationHandle<T, R> continuation, T answer) => ResumeContinuation(continuation, answer);

        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static void ExecControlDelegateA<T>(Action<T> d, T x) => d (x);

    }

}
