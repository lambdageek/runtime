// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace HelloWorld
{
    internal class Program
    {
        private static void Main(string[] args)
        {

	    int r = Mono.DelimitedContinuations.Delimit (static () => {
                Demo();
		return 1;
	    });
	    Console.WriteLine ("After delimiter, answer {0}", r);
        }

	private static Mono.DelimitedContinuations.ContinuationHandle<string> ConvertActionToCont(Action<string> action) {
	    var capturedCont = Mono.DelimitedContinuations.TransferControl<Mono.DelimitedContinuations.ContinuationHandle<string>>( (retK) => {
		string arg = Mono.DelimitedContinuations.TransferControl<string> ((callFKont) => {
                    retK.Resume(callFKont);
		});
		action (arg);
		/* act must not return! */
	    });
	    return capturedCont;
	}

	private static int ThreadMain()
	{
	    Console.WriteLine ("A");
	    Yield();
	    Console.WriteLine ("B");
	    return 1;
	}

	private static void Demo()
	{
	    int dummy = Mono.DelimitedContinuations.Delimit<int>(static () => {
		ThreadCreate(ThreadMain);
		ThreadCreate(ThreadMain);
		JoinAllThreads();
		return 0;
	    });
	}

	private static Queue<Mono.DelimitedContinuations.ContinuationHandle<int>> _queue = new ();
	private static Mono.DelimitedContinuations.ContinuationHandle<int> _after_join_K = default;

	private static void ThreadCreate(Func<int> threadBody)
	{
	    _queue.Enqueue(Mono.DelimitedContinuations.TransferControl<Mono.DelimitedContinuations.ContinuationHandle<int>> ((enqueueK) => {
                /* Capture the continuation before calling the thread body */
		int dummy = Mono.DelimitedContinuations.TransferControl<int> ((beforeCallK) => {
		    enqueueK.Resume(beforeCallK);
		});
		int dummy2 = threadBody ();
		/* after the thread completes either run the next suspended thread or return to the join point. */
		if (_queue.Count == 0) {
		    _after_join_K.Resume(123);
		} else {
                    _queue.Dequeue().Resume(456);
                }
            }));
        }

        private static void Yield()
        {
            Mono.DelimitedContinuations.TransferControl<int> ((afterYieldK) => {
                // save the contiunation for the current thread
                _queue.Enqueue (afterYieldK);
                // and resume some other thread's continuation
                _queue.Dequeue().Resume(789);
            });
        }

	private static void JoinAllThreads ()
	{
	    Mono.DelimitedContinuations.TransferControl<int> ((afterJoinK) => {
		_after_join_K = afterJoinK;
		_queue.Dequeue().Resume(999);
	    });
	}

    }

}
