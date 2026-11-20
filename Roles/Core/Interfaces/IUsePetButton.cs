namespace TownOfHostL.Roles.Core.Interfaces;

/// <summary>
/// ペットをかわいがって能力を使う役職。
/// ホスト自身の操作も客の Pet の電文も <see cref="Modules.PetAbility"/> の 1 つの入口を通り、ここへ届く。
/// 押した時の受け付けの間隔 (連打の間引き)・生死・ベントやはしごの中かは入口で見る。役職のクールは役職が持つ。
/// </summary>
public interface IUsePetButton
{
    /// <summary>今ペットで発動するか。false の間は押しても <see cref="OnPet"/> を呼ばない (設定や形態で切り替わる役職用)</summary>
    public bool UsesPet => true;

    /// <summary>ペットをかわいがった時 (ホストのみ・生きている間だけ)</summary>
    public void OnPet();
}
