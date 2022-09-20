// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace HelloWorld
{
    internal class Program
    {
        private static void Main(string[] args)
        {

	    Console.WriteLine ("before delimiting");
	    int r = Mono.Control.Delimited.Delimit (static () => {
                Console.WriteLine ("In delimited call");
#if false
		Console.WriteLine ("before capturing continuation");
		int resumedAns = Mono.Control.Delimited.TransferControl<int> (static (cont) => {
		    Console.WriteLine ("In the control handler");
		    Mono.Control.Delimited.ResumeContinuation (cont, 42);
		});
		Console.WriteLine ("After continuation resumed with value {0}", resumedAns);
		return resumedAns + 1;
#else
		return 1;
#endif

	    });
	    Console.WriteLine ("After delimiter, answer {0}", r);
        }

	private static Mono.Control.Delimited.ContinuationHandle<string> ConvertActionToCont(Action<string> action) {
	    var capturedCont = Mono.Control.Delimited.TransferControl<Mono.Control.Delimited.ContinuationHandle<string>>( (retK) => {
		string arg = Mono.Control.Delimited.TransferControl<string> ((callFKont) => {
		    Mono.Control.Delimited.ResumeContinuation (retK, callFKont);
		    });
		action (arg);
		/* act must not return! */
	    });
	    return capturedCont;
	}
    }

}
