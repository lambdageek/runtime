// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using Filaments;

namespace Sample
{
    public partial class Test
    {
        public static int Main(string[] args)
        {
            return 0;
        }

        [JSImport("Sample.Test.displayMessage", "main.js")]
        static partial void DisplayMessage(string meaning);

        [JSExport]
        public static void DemoSync()
        {
            ThreadMain();
        }

        [JSExport]
        public static Task Demo()
        {
            Filament.OnYield = () =>
            {
                DisplayMessage($"Yielded after {CallCount} calls");
            };
            Filament t = Filament.Run (ThreadMain);
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
                long answer = SlowFib(N);
                DisplayMessage($"iteration {i} computed {answer}");
            }
        }

        public static long SlowFib (int n)
        {
            CallCount++;
            Filament.MaybeYield();
            if (n <= 1)
                return 1;
            else
                return SlowFib (n - 1)  + SlowFib (n - 2);
        }


        [JSImport("Sample.Test.updateTick", "main.js")]
        static partial void UpdateTick(string message);

        [JSExport]
        public static async Task Tick()
        {
            string[] pix = { "🅰", "🅱", "🅲", "🅳", "🅴", "🅵", "🅶", "🅷", "🅸", "🅹", "🅺", "🅻", "🅼", "🅽", "🅾", "🅿︎", "🆀", "🆁", "🆂", "🆃", "🆄", "🆅", "🆆", "🆇", "🆈", "🆉" };
            int i = 0; ;
            while (true)
            {
                UpdateTick(pix[i]);
                if (++i >= pix.Length)
                {
                    i = 0;
                }
                await Task.Delay(500);
            }
        }
    }
}
