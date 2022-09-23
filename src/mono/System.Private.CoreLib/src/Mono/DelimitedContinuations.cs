// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Mono;

public static partial class DelimitedContinuations
{

    // Represents a captured continuation from a top frame accepting a value TCont.
    // If continuations were first class objects, this would just be Action<TCont>.
    // The continuations are one-shot, so they can only be resumed once.
    public struct ContinuationHandle<TCont>
    {
        private readonly IntPtr _value; // this is a pointer into some saved continuation table in the runtime
        public IntPtr Value { get => _value; init { _value = value; } }

        /// Given an answer to give to the continuation, resumes the continuation by placing it back
        /// as the active stack. The rest of the current computation following the call to Resume is
        /// abandoned.
        [DoesNotReturn]
        public void Resume (TCont? answer) => ResumeContinuation (Value, (object?)answer);
    }

    // Represents a captured continuation from a top frame that does not return a value.
    // If continuations were first class objects, this would just be Action.
    // The continuations are one-shot, so they can only be resumed once.
    public struct ContinuationHandle
    {
        private readonly IntPtr _value; // this is a pointer into some saved continuation table in the runtime
        public IntPtr Value { get => _value; init { _value = value; } }

        /// Given an answer to give to the continuation, resumes the continuation by placing it back
        /// as the active stack. The rest of the current computation following the call to Resume is
        /// abandoned.
        [DoesNotReturn]
        public void Resume() => ResumeContinuation(Value, (object?)null);
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
        IntPtr continuation = IntPtr.Zero;
        CaptureContinuation(ref continuation, ref answer);
        if (continuation != IntPtr.Zero) {
            // This branch runs on a fresh stack and must not return from TransferControl.
            // In fact the continuationConsumer must finish by calling Resume on some continuation - it return normally or throw.
            // Changes to locals will not be reflected back in the resumed continuation.
            RegisterCapturedContinuation (continuation);
            continuationConsumer (new ContinuationHandle<T> {Value = continuation });
            Environment.FailFast ("TransferControl<T> continuation consumer must not return!");
            // this doesn't return, but there's no good way to convince Mono of that - adding throw null here confuses the interpreter
        }
        return (T?)answer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TransferControl(Action<ContinuationHandle> continuationConsumer)
    {
        object? dummyAnswer = null;
        IntPtr continuation = IntPtr.Zero;
        CaptureContinuation(ref continuation, ref dummyAnswer);
        if (continuation != IntPtr.Zero) {
            RegisterCapturedContinuation(continuation);
            continuationConsumer(new ContinuationHandle { Value = continuation });
            Environment.FailFast("TransferControl continuation consumer must not return!");
            // this doesn't return, but there's no good way to convince Mono of that - adding throw null here confuses the interpreter
        }
    }

    private static readonly ContinuationAccounting _accounting = new ();

    private static void RegisterCapturedContinuation (IntPtr continuationPtr) => _accounting.Register (continuationPtr);

    private static bool TryUnregisterCapturedContinuation (IntPtr continuationPtr) => _accounting.Unregister (continuationPtr);

    [Intrinsic]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CaptureContinuation (ref IntPtr continuationDest, ref object? answerDest) => throw null!;

    [DoesNotReturn]
    private static void ResumeContinuation(IntPtr continuation, object? answer)
    {
        if (!TryUnregisterCapturedContinuation (continuation))
            Environment.FailFast (string.Format ("Cannot resume continuation 0x{0:x}, not found", continuation));
        ResumeContinuation_Internal (continuation, answer);
    }

    [DoesNotReturn]
    [Intrinsic]
    private static void ResumeContinuation_Internal (IntPtr continuation, object? answer) => ResumeContinuation_Internal(continuation, answer);

    private sealed class ContinuationAccounting
    {
        private readonly Dictionary<IntPtr, int> _knownContinuations = new (); // continuation to the thread id where it was captured
        // FIXME: Delimit should reset the root continuation
        private readonly Dictionary<int, IntPtr> _rootContinuations = new (); // ManagedThreadId to continuation ids (or IntPtr.Zero)

        internal void Register(IntPtr continuation)
        {
            int threadId = Environment.CurrentManagedThreadId;
            if (!_knownContinuations.TryAdd(continuation, threadId))
                Environment.FailFast(string.Format("Continuation 0x{0:x} already registered", continuation));
            if (!_rootContinuations.TryGetValue(threadId, out IntPtr prevRootContinuation) || prevRootContinuation == IntPtr.Zero)
                _rootContinuations[threadId] = continuation;
        }

        internal bool Unregister (IntPtr continuation)
        {
            if (!_knownContinuations.TryGetValue(continuation, out int capturedOnThreadId))
                return false;

            // FIXME: what if two different threads restore this continuation at the same time - need to lock.

            int threadId = Environment.CurrentManagedThreadId;

            if (!_rootContinuations.TryGetValue(threadId, out IntPtr rootContinuation) || rootContinuation == IntPtr.Zero)
                Environment.FailFast(string.Format ("Cannot abandond the main continuation of thread {0}", threadId));
            Debug.Assert(rootContinuation != IntPtr.Zero);

            if (capturedOnThreadId != threadId && _rootContinuations.TryGetValue(capturedOnThreadId, out IntPtr rootOfCapturedThread)
                && rootOfCapturedThread == continuation)
                Environment.FailFast(string.Format("Cannot restore the root continuation 0x{0:x} of thread {1} on a different thread {2}", continuation, capturedOnThreadId, threadId));

            _knownContinuations.Remove(continuation);

            if (rootContinuation == continuation)
            {
                Debug.Assert(capturedOnThreadId == threadId);
                _rootContinuations[threadId] = IntPtr.Zero; // ok, restored the root continuation
            }

            return true;
        }
    }

}
