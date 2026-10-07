using UnityEngine;

// 操作キャラの服装をモデル全体、または共通モデル内のパーツの表示・非表示で切り替える。
// ラックの表示やクリックはDoorInteractionが担当し、完了イベントから呼び出す。
[DefaultExecutionOrder(-10500)]
public class CharacterOutfitController : MonoBehaviour
{
    public enum OutfitSwitchMode { WholeModel, ShoesParts, AllParts }

    [SerializeField] private CameraSwitch cameraSwitch;
    [SerializeField] private PlayerMovement playerMovement;
    [Tooltip("Whole Model：従来のモデル全体。Shoes Parts：寝巻／スーツはモデル全体、靴だけパーツ。All Parts：共通モデル内で服も靴もパーツ切り替え。")]
    [SerializeField] private OutfitSwitchMode switchMode;
    [Header("モデル全体の切り替え（Whole Model / Shoes Parts）")]
    [Tooltip("寝巻姿の骨・体・服・Animatorをまとめた見た目専用の親。")]
    [SerializeField] private GameObject pajamaStyle;
    [Tooltip("スーツ姿の骨・体・服・Animatorをまとめた見た目専用の親。展示用の服ではありません。")]
    [SerializeField] private GameObject suitStyle;
    [Tooltip("任意。靴を履いたスーツ姿のモデル全体（骨・体・服・Animator）。Suit Styleとは別の兄弟オブジェクトを登録します。")]
    [SerializeField] private GameObject suitWithShoesStyle;
    [Header("共通モデル（All Parts）")]
    [Tooltip("体・骨・Animatorと切り替える全パーツを含む見た目専用の親。操作スクリプトやカメラは外に置きます。")]
    [SerializeField] private GameObject sharedStyle;
    [Tooltip("All Parts：寝巻のときだけ有効にするGameObject。共通の体や骨は登録しません。")]
    [SerializeField] private GameObject[] pajamaParts = new GameObject[0];
    [Tooltip("All Parts：スーツのときだけ有効にするGameObject。靴・素足は下の専用リストへ登録します。")]
    [SerializeField] private GameObject[] suitParts = new GameObject[0];
    [Header("靴のパーツ（Shoes Parts / All Parts）")]
    [Tooltip("靴を履いたときだけ有効にするGameObject。靴・靴ひもなどを複数登録できます。")]
    [SerializeField] private GameObject[] shoeParts = new GameObject[0];
    [Tooltip("裸足のときだけ有効にするGameObject。足が体と一体なら空欄も可能です。")]
    [SerializeField] private GameObject[] barefootParts = new GameObject[0];
    private bool initialized;
    public bool IsWearingSuit { get; private set; }
    public bool IsWearingShoes { get; private set; }

    // 起床演出が体を隠すより前に初期服装とRendererの元の状態を確定する。
    private void Awake() => Initialize();

    // コンポーネントを追加した時点で旧CameraSwitchの参照をInspectorに引き継ぐ。
    private void Reset() => ResolveReferences();

    private void ResolveReferences()
    {
        if (cameraSwitch == null) cameraSwitch = GetComponent<CameraSwitch>();
        if (cameraSwitch == null) cameraSwitch = GetComponentInParent<CameraSwitch>();
        if (cameraSwitch != null)
        {
            cameraSwitch.GetLegacyOutfitReferences(out PlayerMovement movement, out GameObject pajamas, out GameObject suit);
            if (playerMovement == null) playerMovement = movement;
            if (pajamaStyle == null) pajamaStyle = pajamas;
            if (suitStyle == null) suitStyle = suit;
        }
        if (playerMovement == null) playerMovement = GetComponentInParent<PlayerMovement>();
    }

    // CameraSwitchから先に呼ばれても、Awakeと二重に初期化しない。
    public void Initialize()
    {
        if (initialized) return;
        ResolveReferences();
        if (!ValidateSetup()) return;
        initialized = true;
        ApplyOutfit(false);
    }

    private bool ValidateSetup()
    {
        if (switchMode == OutfitSwitchMode.AllParts)
        {
            if (cameraSwitch == null || playerMovement == null || !IsSeparateModel(sharedStyle))
                return SetupError("Shared StyleにAnimatorを含む見た目専用の共通モデルを指定してください。操作元とカメラはモデルの外に置きます。");
            return ValidateParts(sharedStyle, true);
        }
        if (cameraSwitch == null || playerMovement == null || pajamaStyle == null || suitStyle == null ||
            pajamaStyle.transform.IsChildOf(suitStyle.transform) || suitStyle.transform.IsChildOf(pajamaStyle.transform) ||
            transform.IsChildOf(pajamaStyle.transform) || transform.IsChildOf(suitStyle.transform) ||
            cameraSwitch.transform.IsChildOf(pajamaStyle.transform) || cameraSwitch.transform.IsChildOf(suitStyle.transform) ||
            playerMovement.transform.IsChildOf(pajamaStyle.transform) || playerMovement.transform.IsChildOf(suitStyle.transform) ||
            pajamaStyle.GetComponentInChildren<Animator>(true) == null || suitStyle.GetComponentInChildren<Animator>(true) == null)
        {
            Debug.LogWarning("CharacterOutfitController：寝巻・スーツの別々のモデルと各Animatorを指定してください。操作元とカメラはモデルの外に置きます。", this);
            return false;
        }
        if (switchMode == OutfitSwitchMode.ShoesParts)
            return (suitWithShoesStyle == null || ValidateShoesStyle()) && ValidateParts(suitStyle, false);
        return suitWithShoesStyle == null || ValidateShoesStyle();
    }

    private bool SetupError(string message)
    {
        Debug.LogWarning("CharacterOutfitController：" + message, this);
        return false;
    }

    private bool IsSeparateModel(GameObject model)
    {
        return model != null && !transform.IsChildOf(model.transform) &&
            !cameraSwitch.transform.IsChildOf(model.transform) && !playerMovement.transform.IsChildOf(model.transform) &&
            model.GetComponentInChildren<Animator>(true) != null;
    }

    // 骨やAnimatorまで無効になる指定と、親子・同一パーツを別状態に登録する競合を再生前に検出する。
    private bool ValidateParts(GameObject model, bool includeClothes)
    {
        if (includeClothes && (pajamaParts == null || pajamaParts.Length == 0) && (suitParts == null || suitParts.Length == 0))
            return SetupError("All PartsではPajama PartsまたはSuit Partsに切り替える服のパーツを登録してください。");
        GameObject[][] groups = includeClothes
            ? new[] { pajamaParts, suitParts, shoeParts, barefootParts }
            : new[] { shoeParts, barefootParts };
        var registered = new System.Collections.Generic.List<GameObject>();
        var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (GameObject[] group in groups)
        {
            if (group == null) continue;
            foreach (GameObject part in group)
            {
                if (part == null) return SetupError("パーツのリストに空欄があります。オブジェクトを指定するか空の要素を削除してください。");
                if (part == model || !part.transform.IsChildOf(model.transform) ||
                    part.GetComponentInChildren<Animator>(true) != null || part.GetComponentInChildren<Camera>(true) != null ||
                    part.GetComponentInChildren<CharacterController>(true) != null)
                    return SetupError(part.name + "は表示切り替え用のパーツにできません。指定モデル内の服・靴などだけを登録してください。");
                foreach (GameObject other in registered)
                    if (part.transform.IsChildOf(other.transform) || other.transform.IsChildOf(part.transform))
                        return SetupError(part.name + "と" + other.name + "が重複または親子関係です。各パーツは1つのリストだけへ登録してください。");
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    if (renderer.rootBone != null && renderer.rootBone.IsChildOf(part.transform))
                        return SetupError(part.name + "にモデルの骨が含まれています。骨は常に有効にし、メッシュ側を登録してください。");
                    foreach (Transform bone in renderer.bones)
                        if (bone != null && bone.IsChildOf(part.transform))
                            return SetupError(part.name + "にモデルの骨が含まれています。骨は常に有効にし、メッシュ側を登録してください。");
                }
                registered.Add(part);
            }
        }
        return true;
    }

    // 共通の操作元や他のモデルまで無効化しないよう、見た目専用の独立したモデルだけを許可する。
    private bool ValidateShoesStyle()
    {
        if (suitWithShoesStyle == null ||
            suitWithShoesStyle.transform.IsChildOf(pajamaStyle.transform) || pajamaStyle.transform.IsChildOf(suitWithShoesStyle.transform) ||
            suitWithShoesStyle.transform.IsChildOf(suitStyle.transform) || suitStyle.transform.IsChildOf(suitWithShoesStyle.transform) ||
            transform.IsChildOf(suitWithShoesStyle.transform) || cameraSwitch.transform.IsChildOf(suitWithShoesStyle.transform) ||
            playerMovement.transform.IsChildOf(suitWithShoesStyle.transform) ||
            suitWithShoesStyle.GetComponentInChildren<Animator>(true) == null)
        {
            Debug.LogWarning("CharacterOutfitController：Suit With Shoes Styleに、Animatorを含む靴を履いたスーツのモデル全体を登録してください。操作元や他のモデルを含めないでください。", this);
            return false;
        }
        return true;
    }

    // UnityEventから引数なしで選べる、服装ごとの入口。
    public void WearSuit() => TrySetSuit(true);
    public void WearPajamas() => TrySetSuit(false);

    // 出発イベントのOn Startedから呼ぶ入口。着靴だけはイベント中も許可し、起床演出中は拒否する。
    public void WearShoes() => TrySetShoes(true);
    public void WearBarefoot() => TrySetShoes(false);

    public bool TrySetShoes(bool wearShoes)
    {
        if (!isActiveAndEnabled) return false;
        Initialize();
        if (!initialized || !ValidateSetup() || cameraSwitch.IntroActive) return false;
        if (!IsWearingSuit)
        {
            Debug.LogWarning("CharacterOutfitController：靴の切り替えはスーツ姿で実行してください。", this);
            return false;
        }
        if (wearShoes)
        {
            if (switchMode == OutfitSwitchMode.WholeModel && !ValidateShoesStyle()) return false;
            if (switchMode != OutfitSwitchMode.WholeModel && (shoeParts == null || shoeParts.Length == 0))
                return SetupError("Shoe Partsに靴・靴ひもなどのパーツを登録してください。");
        }
        Animator nextAnimator = GetStyle(true, wearShoes).GetComponentInChildren<Animator>(true);
        bool hasSpeed = false;
        if (nextAnimator.enabled && nextAnimator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter parameter in nextAnimator.parameters)
                if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float) hasSpeed = true;
        if (!hasSpeed)
        {
            Debug.LogWarning("CharacterOutfitController：切り替え先に、有効なAnimator・移動用Controller・FloatのSpeedが必要です。", this);
            return false;
        }
        ApplyOutfit(true, wearShoes);
        return true;
    }

    // 起床演出のRenderer復元と競合しないよう、演出中の変更を拒否する。
    public bool TrySetSuit(bool wearSuit)
    {
        if (!isActiveAndEnabled) return false;
        Initialize();
        if (!initialized || !ValidateSetup() || cameraSwitch.IntroActive || cameraSwitch.EventActive) return false;
        ApplyOutfit(wearSuit, wearSuit && IsWearingShoes);
        return true;
    }

    // 一人称で新しい体が一瞬映らないよう、表示状態を登録してからモデルを有効にする。
    private void ApplyOutfit(bool wearSuit, bool wearShoes = false)
    {
        GameObject nextStyle = GetStyle(wearSuit, wearShoes);
        if (switchMode == OutfitSwitchMode.AllParts)
            cameraSwitch.RememberStyle(sharedStyle);
        else
        {
            cameraSwitch.RememberStyle(pajamaStyle);
            cameraSwitch.RememberStyle(suitStyle);
            cameraSwitch.RememberStyle(suitWithShoesStyle);
        }
        playerMovement.animator = nextStyle.GetComponentInChildren<Animator>(true);
        if (switchMode == OutfitSwitchMode.AllParts)
        {
            SetParts(pajamaParts, !wearSuit);
            SetParts(suitParts, wearSuit);
            SetParts(shoeParts, wearSuit && wearShoes);
            SetParts(barefootParts, !wearSuit || !wearShoes);
            sharedStyle.SetActive(true);
        }
        else
        {
            if (switchMode == OutfitSwitchMode.ShoesParts)
            {
                SetParts(shoeParts, wearSuit && wearShoes);
                SetParts(barefootParts, !wearShoes);
            }
            suitStyle.SetActive(wearSuit && (switchMode == OutfitSwitchMode.ShoesParts || !wearShoes));
            if (suitWithShoesStyle != null)
                suitWithShoesStyle.SetActive(switchMode == OutfitSwitchMode.WholeModel && wearSuit && wearShoes);
            pajamaStyle.SetActive(!wearSuit);
        }
        IsWearingSuit = wearSuit;
        IsWearingShoes = wearSuit && wearShoes;
        // イベントが古いAnimatorを保持したままだと、新しい体が歩かずRoot Motionも競合する。
        EventSequenceController.NotifyPlayerAnimatorChanged(playerMovement);
    }

    private GameObject GetStyle(bool wearSuit, bool wearShoes)
    {
        if (switchMode == OutfitSwitchMode.AllParts) return sharedStyle;
        if (!wearSuit) return pajamaStyle;
        return switchMode == OutfitSwitchMode.WholeModel && wearShoes ? suitWithShoesStyle : suitStyle;
    }

    // GameObjectの有効状態で服装を保持する。カメラがRendererを再表示しても脱いだ服は復活しない。
    private static void SetParts(GameObject[] parts, bool visible)
    {
        if (parts == null) return;
        foreach (GameObject part in parts)
            if (part != null && part.activeSelf != visible) part.SetActive(visible);
    }
}
