namespace TownOfHost.Roles.Core.Interfaces;
/// <summary>
/// ペットで発動する能力を持つ役職
/// クールダウン処理は各役職のコード側なので注意
/// </summary>
public interface IPetTrigger
{
    /// <summary>
    /// ペット使用時に呼ばれる
    /// </summary>
    /// <returns>ペット撫でをキャンセルするか</returns>
    public bool OnPetUsed();
}
