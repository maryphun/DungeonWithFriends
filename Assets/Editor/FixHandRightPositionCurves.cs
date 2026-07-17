using UnityEditor;
using UnityEngine;

public static class FixHandRightPositionCurves
{
    private const string PivotName = "RightHandPivot";
    private const string HandPath = "RightHandPivot/HandRight";

    [MenuItem("Tools/Animation/Fix Reparented HandRight Positions")]
    private static void FixSelectedClips()
    {
        Transform pivot = FindPivot();

        if (pivot == null)
        {
            EditorUtility.DisplayDialog(
                "RightHandPivot Not Found",
                "Open the character scene and make sure an active object named " +
                "'RightHandPivot' exists.",
                "OK"
            );

            return;
        }

        if (pivot.localRotation != Quaternion.identity)
        {
            EditorUtility.DisplayDialog(
                "Invalid Pivot Rotation",
                "RightHandPivot must have Rotation 0, 0, 0 before running this tool.",
                "OK"
            );

            return;
        }

        if (!ApproximatelyOne(pivot.localScale))
        {
            EditorUtility.DisplayDialog(
                "Invalid Pivot Scale",
                "RightHandPivot must have Scale 1, 1, 1 before running this tool.",
                "OK"
            );

            return;
        }

        AnimationClip[] clips =
            Selection.GetFiltered<AnimationClip>(
                SelectionMode.DeepAssets
            );

        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "No Animation Clips Selected",
                "Select your .anim files or their folder in the Project window.",
                "OK"
            );

            return;
        }

        Vector3 pivotOffset = pivot.localPosition;

        bool confirmed = EditorUtility.DisplayDialog(
            "Fix HandRight Position Curves",
            $"RightHandPivot offset:\n{pivotOffset}\n\n" +
            $"Selected clips: {clips.Length}\n\n" +
            "This subtracts the pivot offset from every HandRight position key.\n\n" +
            "Run this only once. Commit or back up the project first.",
            "Fix Clips",
            "Cancel"
        );

        if (!confirmed)
        {
            return;
        }

        int modifiedClips = 0;
        int modifiedCurves = 0;
        int skippedClips = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (AnimationClip clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(clip);

                if (!assetPath.EndsWith(".anim"))
                {
                    Debug.LogWarning(
                        $"Skipped imported or read-only clip: {clip.name}",
                        clip
                    );

                    skippedClips++;
                    continue;
                }

                int changes = FixClip(clip, pivotOffset);

                if (changes <= 0)
                {
                    continue;
                }

                modifiedClips++;
                modifiedCurves += changes;

                EditorUtility.SetDirty(clip);

                Debug.Log(
                    $"Fixed {changes} HandRight position curves in {clip.name}.",
                    clip
                );
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Finished",
            $"Modified clips: {modifiedClips}\n" +
            $"Modified curves: {modifiedCurves}\n" +
            $"Skipped clips: {skippedClips}",
            "OK"
        );
    }

    private static int FixClip(
        AnimationClip clip,
        Vector3 pivotOffset
    )
    {
        int changes = 0;

        EditorCurveBinding[] bindings =
            AnimationUtility.GetCurveBindings(clip);

        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.path != HandPath)
            {
                continue;
            }

            float offset;

            switch (binding.propertyName)
            {
                case "m_LocalPosition.x":
                    offset = pivotOffset.x;
                    break;

                case "m_LocalPosition.y":
                    offset = pivotOffset.y;
                    break;

                case "m_LocalPosition.z":
                    offset = pivotOffset.z;
                    break;

                default:
                    continue;
            }

            AnimationCurve curve =
                AnimationUtility.GetEditorCurve(clip, binding);

            if (curve == null)
            {
                continue;
            }

            Keyframe[] keys = curve.keys;

            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value -= offset;
            }

            curve.keys = keys;

            Undo.RecordObject(
                clip,
                "Fix Reparented HandRight Position"
            );

            AnimationUtility.SetEditorCurve(
                clip,
                binding,
                curve
            );

            changes++;
        }

        return changes;
    }

    private static Transform FindPivot()
    {
        GameObject pivotObject =
            GameObject.Find(PivotName);

        return pivotObject != null
            ? pivotObject.transform
            : null;
    }

    private static bool ApproximatelyOne(Vector3 scale)
    {
        return
            Mathf.Approximately(scale.x, 1f) &&
            Mathf.Approximately(scale.y, 1f) &&
            Mathf.Approximately(scale.z, 1f);
    }
}