// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable IntroduceOptionalParameters.Global
// ReSharper disable ConvertToPrimaryConstructor

#pragma warning disable IDE0130
#pragma warning disable CA1069

namespace JetBrains.Annotations;

/// <summary>
/// Marks one configuration-facing member so usage inspections leave it alone.
/// On a type, the same attribute also marks every member, including unused private ones, so it stays on members.
/// </summary>
[MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
[AttributeUsage(AttributeTargets.All, Inherited = false)]
internal sealed class PublicAPIAttribute : Attribute
{
    public PublicAPIAttribute()
    {
    }

    public PublicAPIAttribute(string comment)
    {
        Comment = comment;
    }

    public string? Comment { get; }
}

[AttributeUsage(AttributeTargets.All)]
internal sealed class UsedImplicitlyAttribute : Attribute
{
    public UsedImplicitlyAttribute()
        : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default)
    {
    }

    public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags)
        : this(useKindFlags, ImplicitUseTargetFlags.Default)
    {
    }

    public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags)
        : this(ImplicitUseKindFlags.Default, targetFlags)
    {
    }

    public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
    {
        UseKindFlags = useKindFlags;
        TargetFlags = targetFlags;
    }

    public ImplicitUseKindFlags UseKindFlags { get; }

    public ImplicitUseTargetFlags TargetFlags { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.GenericParameter | AttributeTargets.Parameter)]
internal sealed class MeansImplicitUseAttribute : Attribute
{
    public MeansImplicitUseAttribute()
        : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default)
    {
    }

    public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags)
        : this(useKindFlags, ImplicitUseTargetFlags.Default)
    {
    }

    public MeansImplicitUseAttribute(ImplicitUseTargetFlags targetFlags)
        : this(ImplicitUseKindFlags.Default, targetFlags)
    {
    }

    public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
    {
        UseKindFlags = useKindFlags;
        TargetFlags = targetFlags;
    }

    public ImplicitUseKindFlags UseKindFlags { get; }

    public ImplicitUseTargetFlags TargetFlags { get; }
}

[Flags]
internal enum ImplicitUseKindFlags
{
    Default = Access | Assign | InstantiatedWithFixedConstructorSignature,
    Access = 1,
    Assign = 2,
    InstantiatedWithFixedConstructorSignature = 4,
    InstantiatedNoFixedConstructorSignature = 8
}

[Flags]
internal enum ImplicitUseTargetFlags
{
    Default = Itself,
    Itself = 1,
    Members = 2,
    WithInheritors = 4,
    WithMembers = Itself | Members
}
