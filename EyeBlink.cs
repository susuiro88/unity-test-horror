using UnityEngine;
using System.Collections;

public class EyeBlink : MonoBehaviour
{
    [SerializeField] private GameObject eyeClosePanel;
    [SerializeField] private Material eyeMaterial;
    [SerializeField] private int closedFrames = 4;
    [SerializeField] private CameraSwitch firstPersonMode;//１人称視点かどうかの判別

    private bool isBlinking = false;
    void Start()
    {
        // 最初は非表示
        eyeClosePanel.SetActive(false);

        // 最初は目を開いておく
        eyeMaterial.SetFloat("_eyeOpen", 2f);
    }

    void Update()
    {
        // Qを押している間だけ左クリックを受け付ける
        if (firstPersonMode.FirstPersonMode == true && Input.GetMouseButtonDown(0) && !isBlinking)
        {
            StartCoroutine(Blink());//まばたきのイベント
        }
    }

    IEnumerator Blink()
    {
        isBlinking = true;

        // パネルを表示
        eyeClosePanel.SetActive(true);

        // 2 → 0
        yield return StartCoroutine(ChangeEyeOpen(2f, 0f, 0.15f));

        //待つ
        for (int i = 0; i < closedFrames; i++)
        {
            yield return null;
        }

        // 0 → 2
        yield return StartCoroutine(ChangeEyeOpen(0f, 2f, 0.15f));

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

            Debug.Log("time = " + time + " / value = " + value);

            eyeMaterial.SetFloat("_eyeOpen", value);

            yield return null;
        }

        eyeMaterial.SetFloat("_eyeOpen", end);

        Debug.Log("ChangeEyeOpen終了");
    }
}