using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UndercoverLive;

// 회색 캡슐 사람을 Mixamo 인물로 바꾼다. 캡슐의 콜라이더·Head(시선 기준)는 그대로 두고 렌더러만 끈다.
public static class ChapelCharacters
{
    const string Root = "Assets/_Game/Mixamo";
    const string CtrlDir = "Assets/_Game/Mixamo/Controllers";

    public static string Apply()
    {
        var manCasual = Load("Characters/char_man_casual.fbx");
        var womanCasual = Load("Characters/char_woman_casual.fbx");
        var manSuit = Load("Characters/char_man_suit.fbx");
        var leaderModel = Load("Characters/char_leader_a.fbx") ?? manSuit;     // Joe: 남색 정장
        var staffModel = Load("Characters/char_staff_a.fbx") ?? manSuit;       // Leonard: 검은 옷
        var womanMid = Load("Characters/char_believer_c.fbx") ?? womanCasual;  // Kate
        var manShirt = Load("Characters/char_believer_d.fbx") ?? manCasual;    // Lewis
        if (manCasual == null || womanCasual == null || manSuit == null) return "Mixamo 인물 없음 — 건너뜀";
        var pool = new[] { manCasual, womanCasual, womanMid, manShirt };

        System.IO.Directory.CreateDirectory(CtrlDir);
        var cSit = Controller("Seated", "anim_sitting_idle");
        var cLeader = Controller("Leader", "anim_talking");   // 선 채로 설교
        var cVictim = Controller("Victim", "anim_kneeling_idle");
        var cStaff = Controller("Staff", "anim_standing_idle");

        int n = 0;
        var rnd = new System.Random(11);
        foreach (Transform b in GameObject.Find("Believers").transform)
        {
            var prefab = pool[rnd.Next(pool.Length)];
            // 신도 루트는 의자 높이(0.45)에 있다 → 모델 발은 바닥으로
            Swap(b, prefab, cSit, new Vector3(0, -0.45f, -0.08f), true);
            n++;
        }
        var cast = GameObject.Find("Cast").transform;
        Swap(cast.Find("Leader"), leaderModel, cLeader, Vector3.zero, false);
        Swap(cast.Find("Victim"), manCasual, cVictim, Vector3.zero, false);
        Swap(cast.Find("Staff_Aisle"), staffModel, cStaff, Vector3.zero, false);
        Swap(cast.Find("Staff_Side"), staffModel, cStaff, Vector3.zero, false);
        var trick = GameObject.Find("Trick_PhoneOnly").transform;
        var assistant = trick.Find("Assistant");
        Swap(assistant, staffModel, cStaff, Vector3.zero, false);
        SetLayer(assistant.gameObject, ChapelGreybox.PhoneOnly);   // 조수는 폰에만 보인다

        // 판정 대상이 인물 메쉬를 기준으로 크기를 재도록
        var victim = cast.Find("Victim");
        var vt = victim.GetComponent<CaptureTarget>();
        if (vt != null)
        {
            var list = new List<Renderer>(victim.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            var wire = trick.Find("Wire");
            if (wire != null) list.Add(wire.GetComponent<Renderer>());
            vt.renderers = list.ToArray();
        }
        foreach (var name in new[] { "Leader" })
        {
            var t = cast.Find(name).GetComponent<CaptureTarget>();
            if (t != null) t.renderers = cast.Find(name).GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }
        var at = assistant.GetComponent<CaptureTarget>();
        if (at != null) at.renderers = assistant.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        return $"인물 교체: 신도 {n}명 + 교주·피해 신도·관리자 2·조수";
    }

    static GameObject Load(string rel) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{rel}");

    static AnimatorController Controller(string name, string clipFile)
    {
        string path = $"{CtrlDir}/{name}.controller";
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{Root}/Animations/{clipFile}.fbx"))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) { clip = c; break; }
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var st = ctrl.layers[0].stateMachine.AddState(clipFile);
        st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;
        return ctrl;
    }

    static void Swap(Transform person, GameObject prefab, AnimatorController ctrl, Vector3 localPos, bool offset)
    {
        if (person == null) return;
        foreach (var r in person.GetComponentsInChildren<MeshRenderer>(true))
            if (r.gameObject.name == "Body" || r.gameObject.name == "Head" || r.gameObject.name == "Sash") r.enabled = false;
        var old = person.Find("Model");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, person);
        model.name = "Model";
        // 캡슐 크기 배율을 되돌려 실제 사람 크기로
        var s = person.lossyScale;
        model.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
        model.transform.localPosition = localPos;
        model.transform.localRotation = Quaternion.identity;
        var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (offset) model.AddComponent<AnimStartOffset>();
        // Ch01(평상복 남자)은 머리카락 텍스처가 없어 흰 판으로 보인다 → 머리카락만 끈다
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr.sharedMaterial != null && smr.sharedMaterial.name.StartsWith("Ch01_hair")) smr.enabled = false;
        SetLayer(model, person.gameObject.layer);
    }

    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
    }
}
