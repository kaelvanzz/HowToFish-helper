using System;

namespace FishNet.CodeGenerating;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class ReadUnallocatedAttribute : Attribute
{
}
