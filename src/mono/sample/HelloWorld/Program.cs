// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace HelloWorld
{
    internal class Program
    {
        private static void Main(string[] args)
        {

	    int r = Mono.Control.Delimited.Delimit (static () => {
		Console.WriteLine ("before capturing continuation");
		int resumedAns = Mono.Control.Delimited.TransferControl<int,int> (static (cont) => {
		    Console.WriteLine ("In the control handler");
		    Mono.Control.Delimited.ResumeContinuation (cont, 42);
		});
		Console.WriteLine ("After continuation resumed with value {0}", resumedAns);
		return resumedAns + 1;
	    });
	    Console.WriteLine ("After delimiter, answer {0}", r);
        }
    }
}
