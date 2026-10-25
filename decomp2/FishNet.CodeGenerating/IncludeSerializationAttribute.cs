using System;

namespace FishNet.CodeGenerating;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = false)]
public class IncludeSerializationAttribute : Attribute
{
}
