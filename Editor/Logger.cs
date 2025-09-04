#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor用ログ管理クラス
/// </summary>
public class Logger
{
    private readonly List<string> _lines = new List<string>();      // ログの内部リスト

    /// <summary>
    /// 読み取り専用でログ一覧を取得
    /// </summary>
    public IReadOnlyList<string> Lines => _lines;                  // 外部から参照可能

    /// <summary>
    /// ログを追加し、Unityコンソールにも出力
    /// </summary>
    /// <param name="message">ログ内容</param>
    public void Log(string message)
    {
        _lines.Insert(0, message);                                  // リストの先頭に追加（上から表示される）
    }

    /// <summary>
    /// ログをクリアする
    /// </summary>
    public void Clear()
    {
        _lines.Clear();                                             // リストの全要素を削除
    }
}
#endif
