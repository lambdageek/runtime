// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using DC = Mono.DelimitedContinuations;

namespace Sample
{
    public partial class Test
    {
        public static int Main(string[] args)
        {
            //Demo();
            //DisplayMeaning("42");
            return 0;
        }

        [JSImport("Sample.Test.displayMeaning", "main.js")]
        static partial void DisplayMeaning(string meaning);

#if true
        [JSExport]
        public static void DemoSync()
        {
            ThreadMain();
        }

        const int WorkSlice = 100;  // give the async version 100ms to do work between yields


        [JSExport]
        [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
        public static Task Demo()
        {
            GreenThread t = GreenThread.RunAsGreenThread (ThreadMain);
            return t.Task;
        }

        private static int CurrentIteration = 0;
        private static long CallCount = 0;

        public static void ThreadMain ()
        {
            const int TotalIterations = 10;
            const int N = 25;
            for (int i = 0; i < TotalIterations; i++) {
                CallCount = 0;
                CurrentIteration = i;
                Console.WriteLine ($"running iteration {i}");
                long answer = SlowFib (N);
                Console.WriteLine ($"iteration {i} computed {answer}");
                DisplayMeaning ($"iteration {i} computed {answer}");
            }
        }

        public static long SlowFib (int n)
        {
            CallCount++;
            MaybeYield();
            if (n <= 1)
                return 1;
            else
                return SlowFib (n - 1)  + SlowFib (n - 2);
        }

        static private DateTime lastYield = DateTime.UtcNow;

        public static void MaybeYield()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastYield).TotalMilliseconds > WorkSlice) {
                DisplayMeaning ($"Yielding in iteration {CurrentIteration}, after {CallCount} recursive calls");
                Scheduler.YieldCurrent();
                // set when we resume!
                lastYield = DateTime.UtcNow;
            }
        }
#endif

#if false
	private static int ThreadMain()
	{
	    Console.WriteLine ("A");
	    Yield();
	    Console.WriteLine ("B");
	    return 1;
	}

	private static void Demo()
	{
	    int dummy = DC.Delimit<int>(static () => {
		ThreadCreate(ThreadMain);
		ThreadCreate(ThreadMain);
		JoinAllThreads();
		return 0;
	    });
	}

	private static Queue<DC.ContinuationHandle<int>> _queue = new ();
	private static DC.ContinuationHandle<int> _after_join_K = default;

	private static void ThreadCreate(Func<int> threadBody)
	{
	    _queue.Enqueue(DC.TransferControl<DC.ContinuationHandle<int>> ((enqueueK) => {
                /* Capture the continuation before calling the thread body */
		int dummy = DC.TransferControl<int> ((beforeCallK) => {
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
            DC.TransferControl<int> ((afterYieldK) => {
                // save the contiunation for the current thread
                _queue.Enqueue (afterYieldK);
                // and resume some other thread's continuation
                _queue.Dequeue().Resume(789);
            });
        }

	private static void JoinAllThreads ()
	{
	    DC.TransferControl<int> ((afterJoinK) => {
		_after_join_K = afterJoinK;
		_queue.Dequeue().Resume(999);
	    });
	}
#endif

    }


    // Just a hint to the programmer that the method is in a continuation
    [AttributeUsage(AttributeTargets.Method)]
    public class InContinuationAttribute : Attribute {
        public InContinuationAttribute () { }
    }

    public class GreenThread {
        public Task Task { get; init; }
        internal DC.ContinuationHandle CurrentContinuation { get; private set; }
        internal DC.ContinuationHandle ReturnToSchedulerContinuation {get ; private set; }

        // a convenience
        private readonly static DC.ContinuationHandle ZeroContinuation = default;

        private GreenThread (Task completed) {
            Task = completed;
            CurrentContinuation = ZeroContinuation;
            ReturnToSchedulerContinuation = ZeroContinuation;
        }

        public static GreenThread RunAsGreenThread (Action threadFunc) {
            TaskCompletionSource tcs = new ();
            GreenThread t = new (tcs.Task);
            DC.ContinuationHandle startCont = DC.TransferControl<DC.ContinuationHandle> ((enqueueK) => {
                DC.TransferControl ((beforeCallK) => {
                    enqueueK.Resume (beforeCallK);
                });
                threadFunc();
                tcs.SetResult();
                t.ReturnToScheduler();
            });
            t.CurrentContinuation = startCont;
            Scheduler.EnqueueNew (t);
            return t;
        }

        [InContinuation]
        private void ReturnToScheduler()
        {
            var retK = ReturnToSchedulerContinuation;
            ReturnToSchedulerContinuation = ZeroContinuation;
            retK.Resume ();
        }


        [InContinuation]
        public void Yield() {
            DC.TransferControl ((afterYieldK) => {
                CurrentContinuation = afterYieldK;
                Scheduler.EnqueueResume (this);
                ReturnToScheduler ();
            });
        }

        [InContinuation]
        internal void Execute () {
            DC.TransferControl((returnToSchedulerK) => {
                var computeK = CurrentContinuation;
                CurrentContinuation = ZeroContinuation;
                ReturnToSchedulerContinuation = returnToSchedulerK;
                computeK.Resume ();
            });
        }
    }

    public partial class Scheduler {
        private static Queue<GreenThread> Queue { get; } = new();

        public static GreenThread Current {get ; private set; } = null;

        public static void YieldCurrent()
        {
            if (Current != null) {
                GreenThread g = Current;
                Current = null;
                g.Yield ();
            }
        }

#if false
        public static async Task Loop()
        {
            await Task.Delay (1);
            int count = 0;
            while (PumpScheduler(count)) {
                count ++;
                DateTime now = DateTime.UtcNow;
                await Task.Delay(1000);
                DateTime now2 = DateTime.UtcNow;
            }
        }
#endif

        public static bool PumpScheduler(int count){
            if (Queue.TryDequeue (out GreenThread green)) {
                Console.WriteLine ($"Executing scheduler iteration {count}");
                Current = green;
                Current.Execute();
                Current = null;
                return true;
            }
            return false;
        }

        internal static void EnqueueNew (GreenThread work)
        {
            bool startLoop = Queue.Count == 0;
            Queue.Enqueue (work);
            if (startLoop) {
                RequestPumping();
            }
        }

        internal static void EnqueueResume (GreenThread work)
        {
            Queue.Enqueue (work);
        }

        [JSImport("Sample.Scheduler.requestPumping", "main.js")]
        static partial void RequestPumping();

        [JSExport]
        static int PumpOnce(int count)
        {
            return PumpScheduler(count) ? 1 : 0;
        }
    }

}
