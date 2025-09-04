#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor用ログ管理クラス
/// </summary>
public class Logger
{
    private readonly List<string> _lines = new List<string>();      // 表示ログリスト

    // 読み取り専用でログ一覧を取得可能
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>
    /// ログを追加し、Unityコンソールにも出力する
    /// </summary>
    public void Log(string message)
    {
        _lines.Insert(0, message);      // リストの先頭に追加（上から表示される）
    }

    /// <summary>
    /// ログをクリアする
    /// </summary>
    public void Clear() => _lines.Clear();
}
#endif
