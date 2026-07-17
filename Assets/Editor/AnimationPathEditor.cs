using UnityEditor;
using UnityEngine;

public static class AnimationPathRemapper
{
    // Original hierarchy:
    // Character/HandRight
    private const string OldPath = "HandRight";

    // New hierarchy:
    // Character/RightHandPivot/HandRight
    private const string NewPath = "RightHandPivot/HandRight";

    [MenuItem("Tools/Animation/Remap HandRight Paths")]
    private static void RemapSelectedClips()
    {
        AnimationClip[] clips =
            Selection.GetFiltered<AnimationClip>(
                SelectionMode.DeepAssets
            );

        if (clips.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "No Animation Clips Selected",
                "Select your animation clips or their folder in the Project window.",
                "OK"
            );

            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Remap Animation Paths",
            $"Replace:\n{OldPath}\n\nWith:\n{NewPath}\n\n" +
            $"Selected clips: {clips.Length}\n\n" +
            "It is recommended to commit or back up your project first.",
            "Remap",
            "Cancel"
        );

        if (!confirmed)
        {
            return;
        }

        int modifiedClipCount = 0;
        int modifiedBindingCount = 0;
        int skippedClipCount = 0;

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

                // Imported clips inside FBX files normally cannot be
                // modified directly. This tool modifies .anim files.
                if (!assetPath.EndsWith(".anim"))
                {
                    Debug.LogWarning(
                        $"Skipped imported/read-only clip: {clip.name} " +
                        $"({assetPath})",
                        clip
                    );

                    skippedClipCount++;
                    continue;
                }

                Undo.RecordObject(
                    clip,
                    "Remap Animation Binding Paths"
                );

                int changesInClip = 0;

                changesInClip += RemapFloatCurves(clip);
                changesInClip += RemapObjectReferenceCurves(clip);

                if (changesInClip > 0)
                {
                    EditorUtility.SetDirty(clip);

                    modifiedClipCount++;
                    modifiedBindingCount += changesInClip;

                    Debug.Log(
                        $"Remapped {changesInClip} bindings in " +
                        $"{clip.name}.",
                        clip
                    );
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Animation Remapping Finished",
            $"Modified clips: {modifiedClipCount}\n" +
            $"Modified properties: {modifiedBindingCount}\n" +
            $"Skipped imported clips: {skippedClipCount}",
            "OK"
        );
    }

    private static int RemapFloatCurves(AnimationClip clip)
    {
        int changedCount = 0;

        EditorCurveBinding[] bindings =
            AnimationUtility.GetCurveBindings(clip);

        foreach (EditorCurveBinding oldBinding in bindings)
        {
            if (!TryRemapPath(oldBinding.path, out string newPath))
            {
                continue;
            }

            AnimationCurve curve =
                AnimationUtility.GetEditorCurve(
                    clip,
                    oldBinding
                );

            EditorCurveBinding newBinding = oldBinding;
            newBinding.path = newPath;

            // Delete the old missing binding.
            AnimationUtility.SetEditorCurve(
                clip,
                oldBinding,
                null
            );

            // Add the same animation curve at the new path.
            AnimationUtility.SetEditorCurve(
                clip,
                newBinding,
                curve
            );

            changedCount++;
        }

        return changedCount;
    }

    private static int RemapObjectReferenceCurves(
        AnimationClip clip
    )
    {
        int changedCount = 0;

        EditorCurveBinding[] bindings =
            AnimationUtility.GetObjectReferenceCurveBindings(
                clip
            );

        foreach (EditorCurveBinding oldBinding in bindings)
        {
            if (!TryRemapPath(oldBinding.path, out string newPath))
            {
                continue;
            }

            ObjectReferenceKeyframe[] keyframes =
                AnimationUtility.GetObjectReferenceCurve(
                    clip,
                    oldBinding
                );

            EditorCurveBinding newBinding = oldBinding;
            newBinding.path = newPath;

            // Remove the old sprite/object binding.
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                oldBinding,
                null
            );

            // Restore it using the new hierarchy path.
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                newBinding,
                keyframes
            );

            changedCount++;
        }

        return changedCount;
    }

    private static bool TryRemapPath(
        string currentPath,
        out string remappedPath
    )
    {
        // HandRight itself.
        if (currentPath == OldPath)
        {
            remappedPath = NewPath;
            return true;
        }

        // Anything previously underneath HandRight:
        //
        // HandRight/Sword
        // becomes
        // RightHandPivot/HandRight/Sword
        string childPrefix = OldPath + "/";

        if (currentPath.StartsWith(childPrefix))
        {
            remappedPath =
                NewPath +
                currentPath.Substring(OldPath.Length);

            return true;
        }

        remappedPath = currentPath;
        return false;
    }

    [MenuItem(
        "Tools/Animation/Remap HandRight Paths",
        true
    )]
    private static bool ValidateRemapSelectedClips()
    {
        return Selection.GetFiltered<AnimationClip>(
            SelectionMode.DeepAssets
        ).Length > 0;
    }
}