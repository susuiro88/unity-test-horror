using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFade : MonoBehaviour
{
    //暗転用の画像を指定
    [SerializeField] Image fadeImage;

    public IEnumerator Fade()
    {
        Debug.Log("Fade開始");
        //
        fadeImage.color = new Color(0, 0, 0, 1);

        yield return new WaitForSeconds(0.3f);

        fadeImage.color = new Color(0, 0, 0, 0);
    }
}