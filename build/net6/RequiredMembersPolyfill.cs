// D-060 · net6 兼容 polyfill：C# 11 `required` 成员的运行时特性
// 这些特性类型位于 .NET 7+ 的运行时；以 net6 为目标编译时需由本文件补齐（internal ⇒ 每个程序集各自持有，无冲突）。
// 以 net8 代号编译时本文件不产生任何代码（NET7_0_OR_GREATER 已定义）。
#if !NET7_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }

        public bool IsOptional { get; init; }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute
    {
    }
}

#endif
