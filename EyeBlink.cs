using System;
using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(10002)]
public class EyeBlink : MonoBehaviour
{
    public event Action BlinkClosed;
    //public event Action ChaseStart;
    public event Action LongBlinkFinished;

    [SerializeField] private GameObject eyeClosePanel;
    [SerializeField] private Material eyeMaterial;
    [SerializeField] private int closedFrames = 4;
    [SerializeField] private CameraSwitch firstPersonMode;//１人称視点かどうかの判別

    [Header("捕捉成功時の暗転")]
    [SerializeField] private float longDarkDuration = 1.5f;

    // 今回のまばたきだけ、暗転を長くするか
    private bool isLongBlink;
    public void RequestLongBlink()
    {
        isLongBlink = true;
    }

    private bool isBlinking = false;
    private bool introControlsEyes;
    public bool CanPlayIntro => eyeClosePanel != null && eyeMaterial != null;

    // 起床演出では怪物へ通知せず、まぶたの見た目だけを共有する。
    public void SetIntroEyeOpen(float value)
    {
        introControlsEyes = true;
        eyeClosePanel.SetActive(value < 2f);
        eyeMaterial.SetFloat("_eyeOpen", Mathf.Clamp(value, 0f, 2f));
    }

    // 起床後は通常の入力へ戻す。
    public void EndIntro()
    {
        SetIntroEyeOpen(2f);
        introControlsEyes = false;
    }
    void Start()
    {
        if (introControlsEyes) return;
        // 最初は非表示
        eyeClosePanel.SetActive(false);

        // 最初は目を開いておく
        eyeMaterial.SetFloat("_eyeOpen", 2f);
    }

    void LateUpdate()
    {
        if (introControlsEyes) return;
        // Eを押している間だけ左クリックを受け付ける
        if (firstPersonMode.FirstPersonMode == true && GameOptions.Down(GameOptions.Action.Blink) && !isBlinking && !DoorInteraction.IsDoorBusy && !DoorInteraction.ConsumedClick)
        {
            StartCoroutine(Blink());//まばたきのイベント
        }
    }

    IEnumerator Blink()
    {
        isBlinking = true;

        // 前回の捕捉成功状態をリセットする
        isLongBlink = false;

        // パネルを表示
        eyeClosePanel.SetActive(true);

        // 2 → 0
        yield return StartCoroutine(ChangeEyeOpen(2f, 0f, 0.25f));

        //待つ
        for (int i = 0; i < closedFrames; i++)
        {
            while (GameOptions.MenuOpen) yield return null;
            yield return null;
        }

        // 目を完全に閉じた瞬間を他のスクリプトへ通知
        while (GameOptions.MenuOpen) yield return null;
        BlinkClosed?.Invoke();
        // 捕捉成功時だけ、追加で長く暗転する
        if (isLongBlink)
        {
            yield return new WaitForSeconds(longDarkDuration);
        }
        //ChaseStart?.Invoke();

        // 0 → 2
        yield return StartCoroutine(ChangeEyeOpen(0f, 2f, 0.25f));

        // 長い暗転だった場合、MonsterExistenceへ追跡開始を通知する
        if (isLongBlink)
        {
            LongBlinkFinished?.Invoke();
        }

        // 完了したら非表示
        eyeClosePanel.SetActive(false);

        isBlinking = false;
    }

    IEnumerator ChangeEyeOpen(float start, float end, float duration)
    {
        float time = 0f; 
        Debug.Log("ChangeEyeOpen開始");

        while (time < duration)
        {
            time += Time.deltaTime;
            float value = Mathf.Lerp(start, end, time / duration);//startからendまで時間かけて変化していく

            //Debug.Log("time = " + time + " / value = " + value);

            eyeMaterial.SetFloat("_eyeOpen", value);//シェーダー画面の変数をdurationの時間を掛けて加算・減算

            yield return null;
        }

        eyeMaterial.SetFloat("_eyeOpen", end);

        Debug.Log("ChangeEyeOpen終了");
    }
}