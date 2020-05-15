
.assembly extern System.Runtime { auto }
.assembly extern System.Console { auto }
.assembly modifiers { }

.method private hidebysig static int32  Main() cil managed
{
  .entrypoint

  ldstr "Hello World"
  call void [System.Console]System.Console::WriteLine(string)
  ldc.i4 42
  ret
}

