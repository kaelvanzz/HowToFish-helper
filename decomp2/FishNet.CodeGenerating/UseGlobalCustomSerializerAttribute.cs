using System;

namespace FishNet.CodeGenerating;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public class UseGlobalCustomSerializerAttribute : Attribute
{
}
