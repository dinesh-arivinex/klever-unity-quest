using Oculus.Interaction;
using UnityEngine;

namespace Klever.VR
{
    // Extends the authored distance grab without taking over UI or grab input.
    public sealed class QuestLessonModelControls : MonoBehaviour
    {
        private DistanceGrabInteractor[] interactors;
        private OVRCameraRig rig;
        private Transform model;
        private BoxCollider box;
        private Vector3 initialScale;
        private float scale = 1f;
        private DistanceGrabInteractor held;
        private Vector3 heldPoint;
        private LineRenderer beam;
        private Material material;

        public void Configure(Transform visibleModel)
        {
            model = visibleModel;
            box = GetComponent<BoxCollider>();
            initialScale = model.localScale;
            scale = 1f;
            held = null;
            interactors = FindObjectsOfType<DistanceGrabInteractor>(true);
            rig = FindObjectOfType<OVRCameraRig>();
            if (beam != null) return;
            var shader = Shader.Find("UI/Default");
            if (shader == null) return;
            var visual = new GameObject("Lesson model aim");
            visual.transform.SetParent(transform, false);
            beam = visual.AddComponent<LineRenderer>();
            material = new Material(shader);
            beam.sharedMaterial = material;
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = beam.endWidth = 0.004f;
            beam.startColor = beam.endColor = new Color(0.2f, 1f, 0.75f);
            beam.enabled = false;
        }

        private bool IsModel(DistanceGrabInteractable target)
        {
            if (target == null || target.Colliders == null) return false;
            foreach (var collider in target.Colliders)
                if (collider == box) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (beam != null) beam.enabled = false;
            if (model == null || !model.gameObject.activeInHierarchy || box == null ||
                !box.enabled || LessonPause.IsPaused || interactors == null) return;
            DistanceGrabInteractor hover = null;
            DistanceGrabInteractor selected = null;
            foreach (var input in interactors)
            {
                if (input == null || !input.isActiveAndEnabled || input.State == InteractorState.Disabled) continue;
                if (IsModel(input.SelectedInteractable)) { selected = input; break; }
                if (IsModel(input.Interactable)) hover = input;
            }

            var aim = selected ?? hover;
            if (aim == null) { held = null; return; }
            var origin = aim.Origin.position;
            if (selected != null)
            {
                if (held != selected)
                    heldPoint = model.InverseTransformPoint(box.ClosestPoint(aim.HitPoint));
                AdjustModel(origin);
            }
            held = selected;

            if (beam == null) return;
            var endpoint = selected != null ? model.TransformPoint(heldPoint) : aim.HitPoint;
            beam.SetPosition(0, origin);
            beam.SetPosition(1, endpoint);
            beam.enabled = true;
        }

        private void AdjustModel(Vector3 origin)
        {
            if (rig == null || rig.leftHandAnchor == null || rig.rightHandAnchor == null) return;
            var left = (origin - rig.leftHandAnchor.position).sqrMagnitude;
            var right = (origin - rig.rightHandAnchor.position).sqrMagnitude;
            var hand = left < right ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            if (!OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, hand)) return;
            var stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, hand);
            const float deadzone = 0.24f;
            if (Mathf.Max(Mathf.Abs(stick.x), Mathf.Abs(stick.y)) < deadzone) return;
            var center = transform.TransformPoint(box.center);
            var localCenter = model.InverseTransformPoint(center);
            if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
                model.Rotate(Vector3.up, stick.x * 75f * Time.deltaTime, Space.World);
            else
            {
                scale = Mathf.Clamp(scale * Mathf.Exp(stick.y * Time.deltaTime), 0.5f, 2f);
                model.localScale = initialScale * scale;
            }
            model.position += center - model.TransformPoint(localCenter);
            RefitCollider();
        }

        private void RefitCollider()
        {
            var bounds = new Bounds(box.center, Vector3.zero);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                    renderer.CompareTag("notAChild") || renderer.name == "Quad" ||
                    renderer.GetComponentInParent<Canvas>() != null) continue;
                var mesh = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    bounds.Encapsulate(transform.InverseTransformPoint(renderer.transform.TransformPoint(corner)));
                }
            }
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.01f);
        }

        private void OnDisable() { if (beam != null) beam.enabled = false; }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
