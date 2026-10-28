// T3: Hatched's pose source for StudyLsl: the world-space transforms driven by FieldDay XR tracking.
using FieldDay.XR;
using UnityEngine;
using UnityEngine.XR;

namespace PenguinStudy {
    static internal class HatchedLslAdapter {
        static private Transform s_Head, s_Left, s_Right;
        static private float s_NextSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static private void Init() {
            StudyLsl.AppName = "hatched";
            StudyLsl.PoseSource = Sample;
        }

        static private bool Sample(out Pose head, out Pose left, out Pose right) {
            head = left = right = default;
            if (s_Head == null || s_Left == null || s_Right == null) {
                if (Time.unscaledTime < s_NextSearch) return false;
                s_NextSearch = Time.unscaledTime + 1f;
                Find();
                if (s_Head == null || s_Left == null || s_Right == null) return false;
            }
            head = new Pose(s_Head.position, s_Head.rotation);
            left = new Pose(s_Left.position, s_Left.rotation);
            right = new Pose(s_Right.position, s_Right.rotation);
            return true;
        }

        static private void Find() {
            Transform headFallback = null;
            foreach (var t in Object.FindObjectsOfType<XRTrackedTransform>()) {
                switch (t.Node) {
                    case XRNode.CenterEye: s_Head = t.transform; break;
                    case XRNode.Head: headFallback = t.transform; break;
                    case XRNode.LeftHand: s_Left = t.transform; break;
                    case XRNode.RightHand: s_Right = t.transform; break;
                }
            }
            if (s_Head == null) s_Head = headFallback;
            if (s_Head == null && Camera.main != null) s_Head = Camera.main.transform;
        }
    }
}
