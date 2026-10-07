using System;
using UnityEngine;

// 調査と範囲イベントで共用する音声設定。停止対象を明示し、アラームの誤再開を防ぐ。
[Serializable]
public sealed class InteractionAudioAction
{
    public enum Operation { Play = 0, Stop = 1 }
    [Tooltip("Play：音声を開始。Stop：指定したAudioSourceの音声を終了。")]
    public Operation operation;
    public AudioSource source;
    [Tooltip("Play専用。未指定ならAudioSourceに設定済みのClipを使用します。")]
    public AudioClip clip;
    [Tooltip("Play専用。アラームはオン、ドアや着替えの効果音はオフ。")]
    public bool loop;

    // Playは先頭から再生する。停止後に範囲を出ても自動的には再開しない。
    public void Execute()
    {
        if (source == null) return;
        if (operation == Operation.Stop) { source.Stop(); return; }
        if (!source.isActiveAndEnabled) return;
        if (clip != null) source.clip = clip;
        if (source.clip == null) return;
        source.loop = loop;
        source.Play();
    }
}
