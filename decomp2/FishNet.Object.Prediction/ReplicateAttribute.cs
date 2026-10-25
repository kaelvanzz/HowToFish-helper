using System;

namespace FishNet.Object.Prediction;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class ReplicateAttribute : Attribute
{
}
