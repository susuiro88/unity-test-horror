using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ステップの種類に必要な項目だけを表示し、音声や移動の設定を取り違えないようにする。
[CustomPropertyDrawer(typeof(EventSequenceController.Step))]
public sealed class EventSequenceStepDrawer : PropertyDrawer
{
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        yield return "name";
        yield return "type";
        yield return "delay";
        yield return "duration";
        // 削除した種類の保存値を欠番にしているため、表示順ではなく実際の数値を使う。
        var type = (EventSequenceController.StepType)property.FindPropertyRelative("type").intValue;
        switch (type)
        {
            case EventSequenceController.StepType.CameraLook:
                yield return "cameraMode";
                if ((EventSequenceController.ViewMode)property.FindPropertyRelative("cameraMode").intValue != EventSequenceController.ViewMode.FirstPerson)
                {
                    yield return "thirdPersonPositionMode";
                    if ((EventSequenceController.ThirdPersonPositionMode)property.FindPropertyRelative("thirdPersonPositionMode").intValue == EventSequenceController.ThirdPersonPositionMode.PlayerRelative)
                        yield return "thirdPersonPosition";
                }
                yield return "lookDirection";
                var look = (EventSequenceController.LookDirection)property.FindPropertyRelative("lookDirection").intValue;
                if (look == EventSequenceController.LookDirection.DirectionMarker || look == EventSequenceController.LookDirection.LookAtTarget)
                    yield return "lookDirectionTarget";
                if (look == EventSequenceController.LookDirection.LookAtTarget || look == EventSequenceController.LookDirection.LookAtPlayer)
                    yield return "lookTargetOffset";
                if (look == EventSequenceController.LookDirection.NumericAngles)
                { yield return "lookRelative"; yield return "lookRotation"; }
                yield return "movePlayer";
                if (property.FindPropertyRelative("movePlayer").boolValue)
                {
                    yield return "movementStyle";
                    yield return "playerDestination";
                    if (property.FindPropertyRelative("playerDestination").objectReferenceValue == null)
                    { yield return "playerOffset"; yield return "useViewDirection"; }
                    yield return "ignorePlayerY";
                    yield return "faceMovement";
                    yield return "useMovementSpeed";
                }
                yield return "easing";
                break;
            case EventSequenceController.StepType.ObjectTransform:
                yield return "target";
                yield return "relative";
                yield return "move";
                if (property.FindPropertyRelative("move").boolValue) yield return "position";
                yield return "rotate";
                if (property.FindPropertyRelative("rotate").boolValue)
                { yield return "rotation"; yield return "rotationPivot"; }
                yield return "scale";
                if (property.FindPropertyRelative("scale").boolValue) yield return "scaleValue";
                yield return "easing";
                break;
            case EventSequenceController.StepType.RendererVisibility:
                yield return "target";
                yield return "visible";
                yield return "includeChildren";
                break;
            case EventSequenceController.StepType.PlayAudio:
                yield return "audioSource";
                yield return "audioClip";
                yield return "loop";
                yield return "waitForAudio";
                break;
            case EventSequenceController.StepType.StopAudio:
                yield return "audioSource";
                break;
            case EventSequenceController.StepType.Fade:
                yield return "fadeAlpha";
                yield return "easing";
                break;
        }
        yield return "onStarted";
        yield return "onCompleted";
    }

    // UnityEventの展開状態も含めて高さを求め、配列の次の要素に重ならないようにする。
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
        {
            foreach (string field in Fields(property))
                height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true);
            if (IsCameraLook(property)) height += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight;
        }
        return height;
    }

    private static bool IsCameraLook(SerializedProperty property) =>
        (EventSequenceController.StepType)property.FindPropertyRelative("type").intValue == EventSequenceController.StepType.CameraLook;

    // SerializedProperty経由で設定し、UndoやPrefabの差分記録も通常のInspector編集と揃える。
    // 時間や歩行先は維持し、プレイヤー参照は実行時にControllerから取得する。
    private static void SetPlayerFraming(SerializedProperty property)
    {
        property.FindPropertyRelative("cameraMode").intValue = (int)EventSequenceController.ViewMode.ThirdPerson;
        property.FindPropertyRelative("thirdPersonPositionMode").intValue = (int)EventSequenceController.ThirdPersonPositionMode.PlayerRelative;
        property.FindPropertyRelative("thirdPersonPosition").vector3Value = new Vector3(0, 1.5f, -3f);
        property.FindPropertyRelative("lookDirection").intValue = (int)EventSequenceController.LookDirection.LookAtPlayer;
        property.FindPropertyRelative("lookTargetOffset").vector3Value = Vector3.zero;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        string name = property.FindPropertyRelative("name").stringValue;
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, new GUIContent(label.text + "：" + name), true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            foreach (string field in Fields(property))
            {
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                var child = property.FindPropertyRelative(field);
                line.height = EditorGUI.GetPropertyHeight(child, true);
                if (field == "ignorePlayerY")
                    EditorGUI.PropertyField(line, child, new GUIContent("Y座標を参照しない", child.tooltip), true);
                else
                    EditorGUI.PropertyField(line, child, true);
            }
            if (IsCameraLook(property))
            {
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                line.height = EditorGUIUtility.singleLineHeight;
                if (GUI.Button(EditorGUI.IndentedRect(line), new GUIContent("プレイヤーを中央に配置（三人称）",
                    "このステップを三人称・位置(0,1.5,-3)・Look At Playerに設定します。実際のカメラ移動はイベント再生時です。")))
                    SetPlayerFraming(property);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
