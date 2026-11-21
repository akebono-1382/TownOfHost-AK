/// <summary>
/// 会議中シェイプシフトを使う役職
/// </summary>
public interface IUseMeetingShapeshift
{
    /// <summary>
    /// ミーティングシェイプシフトが使えるか
    /// </summary>
    bool CanUseMeetingShapeshift { get; }
    /// <summary>
    /// シェイプ後の処理
    /// </summary>
    /// <param name="target">対象</param>
    void OnMeetingShapeshift(PlayerControl target);
}