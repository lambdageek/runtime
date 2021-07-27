using System;
using System.Runtime.CompilerServices;

public class TestClass {
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static string TargetMethod () {
        Func<string,string> fn = static (string s) => s + s;
		string s = "NEW STRING";
		Console.WriteLine (fn (s));
		return s;
        }
}
