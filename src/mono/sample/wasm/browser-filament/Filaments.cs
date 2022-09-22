using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DC = Mono.DelimitedContinuations;
using System.Runtime.InteropServices.JavaScript;

namespace Filaments;


// Just a hint to the programmer that the method is in a continuation
[AttributeUsage(AttributeTargets.Method)]
public class InContinuationAttribute : Attribute {
    public InContinuationAttribute () { }
}

public class Filament {
    public Task Task { get; init; }
    internal DC.ContinuationHandle CurrentContinuation { get; private set; }
    internal DC.ContinuationHandle ReturnToSchedulerContinuation {get ; private set; }

    // a convenience
    private readonly static DC.ContinuationHandle ZeroContinuation = default;

    private Filament (Task completed) {
        Task = completed;
        CurrentContinuation = ZeroContinuation;
        ReturnToSchedulerContinuation = ZeroContinuation;
    }

    public static Filament Run (Action threadFunc) {
        TaskCompletionSource tcs = new ();
        Filament t = new (tcs.Task);
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

    static private DateTime lastYield = DateTime.UtcNow;

    const int WorkSlice = 100;  // give the async version 100ms to do work between yields

    public static void MaybeYield()
    {
        DateTime now = DateTime.UtcNow;
        if ((now - lastYield).TotalMilliseconds > WorkSlice)
        {
            if (OnYield != null)
                OnYield();
            Scheduler.YieldCurrent();
            // set when we resume!
            lastYield = DateTime.UtcNow;
        }
    }


    [InContinuation]
    private void ReturnToScheduler()
    {
        var retK = ReturnToSchedulerContinuation;
        ReturnToSchedulerContinuation = ZeroContinuation;
        retK.Resume ();
    }

    public static Action OnYield { get; set; }

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
    private static Queue<Filament> Queue { get; } = new();

    public static Filament Current {get ; private set; } = null;

    public static void YieldCurrent()
    {
        if (Current != null) {
            Filament g = Current;
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
        if (Queue.TryDequeue (out Filament green)) {
            Console.WriteLine ($"Executing scheduler iteration {count}");
            Current = green;
            Current.Execute();
            Current = null;
            return true;
        }
        return false;
    }

    internal static void EnqueueNew (Filament work)
    {
        bool startLoop = Queue.Count == 0;
        Queue.Enqueue (work);
        if (startLoop) {
            RequestPumping();
        }
    }

    internal static void EnqueueResume (Filament work)
    {
        Queue.Enqueue (work);
    }

    [JSImport("Filaments.Scheduler.requestPumping", "main.js")]
    static partial void RequestPumping();

    [JSExport]
    static int PumpOnce(int count)
    {
        return PumpScheduler(count) ? 1 : 0;
    }
}
