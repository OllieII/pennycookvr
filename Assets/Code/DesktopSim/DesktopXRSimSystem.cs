#if UNITY_EDITOR || DEVELOPMENT_BUILD
// T0b desktop test rig for Hatched.
// When no headset is active, XRInputUpdateSystem (-8000) leaves XRInputState empty and returns.
// This system disables it while simulating, runs at -7990, and fills XRInputState with a simulated head, two hands
// and grip/trigger buttons, so every downstream system (XRTrackedTransformSystem, grab, tablet)
// runs unchanged. It does nothing while a real headset is active.
//
// Controls (F1 toggles help):
//   Mouse            look (hold Right Mouse to move the selected hand instead)
//   Scroll           selected hand nearer / farther
//   W A S D          step around the play space      Space / C   stand taller / crouch
//   1 / 2            select left / right hand
//   Left Mouse       trigger on selected hand (hold)
//   G                toggle grip on selected hand (snaps hand to nearest grabbable within 1 m)
//   R                reset hands in front of you
//   F2               turn the simulator off / on
using System.Collections.Generic;
using BeauUtil;
using FieldDay;
using FieldDay.HID.XR;
using FieldDay.Systems;
using FieldDay.VRHands;
using FieldDay.XR;
using UnityEngine;
using UnityEngine.XR;

namespace Pennycook.DesktopSim {
    [SysUpdate(GameLoopPhaseMask.DebugUpdate | GameLoopPhaseMask.FixedUpdate | GameLoopPhaseMask.Update | GameLoopPhaseMask.ApplicationPreRender, -7990, AllowExecutionDuringLoad = true)]
    public class DesktopXRSimSystem : SharedStateSystemBehaviour<XRInputState> {
        public float EyeHeight = 1.6f;
        public float MouseSensitivity = 2.5f;
        public float WalkSpeed = 1.2f;
        public float SnapRadius = 1.0f;

        static public bool Enabled = true;

        // The real XR input system. Disabled while simulating so it does not clear our state and
        // flap HeadPresence every phase; re-enabled as soon as a headset becomes active.
        [System.NonSerialized] public XRInputUpdateSystem RealInput;

        private Vector3 m_HeadPos;
        private float m_Yaw, m_Pitch;
        private readonly Vector3[] m_HandLocal = new Vector3[2]; // hand offset in head-yaw space
        private readonly bool[] m_Grip = new bool[2];
        private int m_Selected = 1;
        private ushort m_LastButtonFrame = ushort.MaxValue;
        private bool m_ShowHelp = true;
        private bool m_Active;

        public override void Initialize() {
            m_HeadPos = new Vector3(0, EyeHeight, 0);
            ResetHands();
        }

        public override void ProcessWork(float deltaTime) {
            if (Input.GetKeyDown(KeyCode.F2)) Enabled = !Enabled;
            m_Active = Enabled && !XRSettings.isDeviceActive;
            if (RealInput != null && RealInput.enabled == m_Active) RealInput.enabled = !m_Active;
            if (!m_Active) return;

            XRInputState s = m_State;
            bool isFrameStart = Ref.Replace(ref m_LastButtonFrame, Frame.Index);

            if (isFrameStart) {
                ReadKeyboardAndMouse(Time.unscaledDeltaTime);
            }

            Quaternion yawRot = Quaternion.Euler(0, m_Yaw, 0);
            Quaternion headRot = Quaternion.Euler(m_Pitch, m_Yaw, 0);
            Pose head = new Pose(m_HeadPos, headRot);
            s.Head = head;
            s.CenterEye = head;
            s.LeftEye = new Pose(m_HeadPos + headRot * new Vector3(-0.032f, 0, 0), headRot);
            s.RightEye = new Pose(m_HeadPos + headRot * new Vector3(0.032f, 0, 0), headRot);

            for (int i = 0; i < 2; i++) {
                ref XRHandState hand = ref s.Hand((XRHandIndex) i);
                hand.Pose = new Pose(m_HeadPos + yawRot * m_HandLocal[i], headRot);
                if (isFrameStart) {
                    XRHandButtons b = XRHandButtons.None;
                    if (m_Grip[i]) b |= XRHandButtons.GripButton | XRHandButtons.GripTouch;
                    if (i == m_Selected && Input.GetMouseButton(0)) b |= XRHandButtons.TriggerButton;
                    hand.Buttons.Update(b);
                    hand.Axis.Grip = m_Grip[i] ? 1 : 0;
                    hand.Axis.Trigger = (b & XRHandButtons.TriggerButton) != 0 ? 1 : 0;
                }
            }

            if (s.AvailableSources != XRSourceMask.Everything) {
                s.AvailableSources = XRSourceMask.Everything;
                XRInputUtility.OnAvailableNodesUpdated.Invoke(XRSourceMask.Everything);
            }
            if (!s.HeadPresence) {
                s.HeadPresence = true;
                XRInputUtility.OnUserPresenceUpdated.Invoke(true);
            }
        }

        private void ReadKeyboardAndMouse(float dt) {
            if (Input.GetKeyDown(KeyCode.F1)) m_ShowHelp = !m_ShowHelp;
            if (Input.GetKeyDown(KeyCode.Alpha1)) m_Selected = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) m_Selected = 1;
            if (Input.GetKeyDown(KeyCode.R)) ResetHands();

            float mx = Input.GetAxisRaw("Mouse X") * MouseSensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
            if (Input.GetMouseButton(1)) {
                m_HandLocal[m_Selected] += new Vector3(mx, my, 0) * 0.01f;
            } else {
                m_Yaw += mx;
                m_Pitch = Mathf.Clamp(m_Pitch - my, -85f, 85f);
            }
            m_HandLocal[m_Selected].z = Mathf.Clamp(m_HandLocal[m_Selected].z + Input.mouseScrollDelta.y * 0.05f, 0.1f, 0.9f);

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move += Vector3.back;
            if (Input.GetKey(KeyCode.A)) move += Vector3.left;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            m_HeadPos += Quaternion.Euler(0, m_Yaw, 0) * move * (WalkSpeed * dt);
            if (Input.GetKey(KeyCode.Space)) m_HeadPos.y += dt;
            if (Input.GetKey(KeyCode.C)) m_HeadPos.y -= dt;
            m_HeadPos.y = Mathf.Clamp(m_HeadPos.y, 0.5f, 2.0f);

            if (Input.GetKeyDown(KeyCode.G)) {
                m_Grip[m_Selected] = !m_Grip[m_Selected];
                if (m_Grip[m_Selected]) SnapSelectedHandToNearestGrabbable();
            }
        }

        private void ResetHands() {
            m_HandLocal[0] = new Vector3(-0.2f, -0.35f, 0.4f);
            m_HandLocal[1] = new Vector3(0.2f, -0.35f, 0.4f);
            m_Grip[0] = m_Grip[1] = false;
        }

        // Moves the selected hand onto the closest point of the nearest Grabbable (world space -> tracking space).
        private void SnapSelectedHandToNearestGrabbable() {
            Transform handT = FindHandTransform((XRHandIndex) m_Selected);
            if (handT == null || handT.parent == null) return;
            Vector3 handWorld = handT.position;
            float best = SnapRadius;
            Vector3 bestPoint = handWorld;
            bool found = false;
            foreach (var g in FindObjectsOfType<Grabbable>()) {
                foreach (var col in g.GetComponentsInChildren<Collider>()) {
                    if (!col.enabled || col.isTrigger) continue;
                    Vector3 p = col.ClosestPoint(handWorld);
                    float d = Vector3.Distance(p, handWorld);
                    if (d < best) { best = d; bestPoint = p; found = true; }
                }
            }
            if (!found) return;
            float scale = WorldScale();
            Vector3 trackingPos = handT.parent.InverseTransformPoint(bestPoint) / Mathf.Max(scale, 0.0001f);
            m_HandLocal[m_Selected] = Quaternion.Inverse(Quaternion.Euler(0, m_Yaw, 0)) * (trackingPos - m_HeadPos);
        }

        static private Transform FindHandTransform(XRHandIndex hand) {
            XRNode node = hand == XRHandIndex.Left ? XRNode.LeftHand : XRNode.RightHand;
            foreach (var t in FindObjectsOfType<XRTrackedTransform>()) {
                if (t.Node == node) return t.transform;
            }
            return null;
        }

        static private float WorldScale() {
            var sys = FindObjectOfType<XRTrackedTransformSystem>();
            return sys != null && sys.WorldScale > 0 ? sys.WorldScale : 1f;
        }

        private void OnGUI() {
            if (!m_Active || !m_ShowHelp) return;
            string sel = m_Selected == 0 ? "LEFT" : "RIGHT";
            GUI.Box(new Rect(10, 10, 360, 150),
                "DESKTOP SIM (F1 hide, F2 off)\n" +
                "Mouse look | RMB+mouse move hand | Scroll depth\n" +
                "WASD walk | Space/C height | R reset hands\n" +
                "1/2 select hand: " + sel + "\n" +
                "LMB trigger | G grip toggle (L:" + m_Grip[0] + " R:" + m_Grip[1] + ")");
        }
    }

    /// <summary>
    /// Adds DesktopXRSimSystem next to XRInputUpdateSystem at runtime, so no scene or prefab edits are needed.
    /// </summary>
    static internal class DesktopXRSimBootstrap {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static private void Boot() {
            var host = new GameObject("[DesktopXRSimBootstrap]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Waiter>();
        }

        private class Waiter : MonoBehaviour {
            private float m_Timeout = 30f;
            private void Update() {
                var xr = FindObjectOfType<XRInputUpdateSystem>();
                if (xr != null) {
                    if (xr.GetComponent<DesktopXRSimSystem>() == null) {
                        var sim = xr.gameObject.AddComponent<DesktopXRSimSystem>();
                        sim.RealInput = xr;
                        Game.Systems.Register(sim); // GameLoop processes the init queue next frame
                        Debug.Log("[DesktopXRSim] registered");
                    }
                    Destroy(gameObject);
                } else if ((m_Timeout -= Time.unscaledDeltaTime) <= 0) {
                    Debug.LogWarning("[DesktopXRSim] XRInputUpdateSystem not found; simulator not started");
                    Destroy(gameObject);
                }
            }
        }
    }
}
#endif
