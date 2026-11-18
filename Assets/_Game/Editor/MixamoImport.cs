using System.IO;
using UnityEditor;

// _Game/Mixamo 아래 FBX를 휴머노이드로 가져오고, 동작은 제자리 반복으로 굽는다.
class MixamoImport : AssetPostprocessor
{
    bool Mine => assetPath.Replace('\\', '/').Contains("/_Game/Mixamo/");
    bool IsAnim => assetPath.Replace('\\', '/').Contains("/_Game/Mixamo/Animations/");

    void OnPreprocessModel()
    {
        if (!Mine) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.materialImportMode = IsAnim ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        if (IsAnim) mi.importAnimation = true;
    }

    void OnPreprocessAnimation()
    {
        if (!IsAnim) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        bool oneShot = name == "anim_kneeling";
        foreach (var c in clips)
        {
            c.name = name;
            c.loopTime = !oneShot;
            c.loopPose = !oneShot;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
        }
        mi.clipAnimations = clips;
    }
}
