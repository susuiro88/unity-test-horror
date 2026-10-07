using UnityEngine;

// BGMのAudioSourceに付けてMusicを選ぶ。未指定の音源は効果音として扱う。
[RequireComponent(typeof(AudioSource))]
public sealed class OptionsAudioChannel : MonoBehaviour
{
    public enum Channel { Effects, Music }
    public Channel channel = Channel.Music;
}
