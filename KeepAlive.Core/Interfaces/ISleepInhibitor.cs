namespace KeepAlive.Core.Interfaces;

/// <summary>
/// 操作しないで放置したときの、システムの自動スリープを抑止する (OS 依存。実装は Infrastructure)。
/// 画面の消灯は妨げない。ふたを閉じる、電源ボタンを押すなど、利用者の操作によるスリープは防げない。
/// </summary>
public interface ISleepInhibitor
{
    /// <summary>この OS で抑止できるか。</summary>
    bool IsSupported { get; }

    /// <summary>
    /// 抑止を有効または無効にする。同じ値で何度呼んでもよく、どのスレッドから呼んでもよい。
    /// 抑止できなかった場合は例外を投げる。
    /// </summary>
    void SetEnabled(bool enabled);
}
