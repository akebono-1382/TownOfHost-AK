namespace TownOfHost.Roles.Core.Descriptions;

/// <summary>
/// Mod役職の説明文
/// </summary>
public class SingleRoleDescription : RoleDescription
{
    public SingleRoleDescription(SimpleRoleInfo roleInfo) : base(roleInfo)
    {
        BlurbKey = $"{roleInfo.RoleName}{BlurbSuffix}";
        DescriptionKey = $"{RoleInfo.RoleName}{DescriptionSuffix}";
    }

    /// <summary>短いひとこと説明文の翻訳キー</summary>
    public string BlurbKey { get; }
    /// <summary>長い説明文の翻訳キー</summary>
    public string DescriptionKey { get; }
    public override string Blurb => Translator.GetString(BlurbKey);
    public override string Description
    {
        get
        {
            var desc = Translator.GetString(DescriptionKey);
            if (RoleInfo.RoleName is CustomRoles.Monochromer)
            {
                desc = "\n" + Translator.GetString("SkinWarning") + "\n" + desc;
            }
            else if (RoleInfo.RoleName is CustomRoles.Camouflager)
            {
                desc = "\n" + Translator.GetString("SkinWarning") + "\n" + Translator.GetString("CamoWarn") + "\n" + desc;
            }

            if (RoleInfo.Desc == null) return desc;
            var CustomDescription = RoleInfo.Desc();

            if (CustomDescription == null) return desc;
            return CustomDescription;
        }
    }
    public const string BlurbSuffix = "Info";
    public const string DescriptionSuffix = "InfoLong";
}
