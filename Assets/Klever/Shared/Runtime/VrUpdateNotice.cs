using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Klever.VR.Updates
{
    // Lobby-only world UI; frozen in space while shown so pointing stays stable.
    public sealed class VrUpdateNotice : MonoBehaviour
    {
        private Canvas panel;
        private TextMeshProUGUI heading, detail, actionText;
        private Button primary;
        private Button later;
        private int dismissedVersion;
        private Camera eye;
        private Material overlay;
        private readonly List<Material> textMaterials = new List<Material>();
        private TextMeshProUGUI busyMark;
        private int autoInstallerAttempt;
        private void Awake()
        {
            if (panel != null) return;
            var surface = new GameObject("Klever update notice", typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(TrackedDeviceGraphicRaycaster));
            surface.layer = 5; surface.transform.SetParent(transform, false);
            panel = surface.GetComponent<Canvas>(); panel.renderMode = RenderMode.WorldSpace; panel.sortingOrder = 32000;
            var rect = (RectTransform)surface.transform; rect.sizeDelta = new Vector2(1000, 420); rect.localScale = Vector3.one * 0.001f;
            overlay = new Material(Shader.Find("UI/Default")); overlay.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always); overlay.renderQueue = 4990;
            var image = surface.GetComponent<Image>(); image.color = new Color(0.025f, 0.055f, 0.08f, 0.98f); image.material = overlay; image.raycastTarget = true;
            heading = Label(surface.transform, "Update title", new Vector2(0, 135), new Vector2(920, 70), 42);
            detail = Label(surface.transform, "Update detail", new Vector2(0, 30), new Vector2(920, 130), 32);
            busyMark = Label(surface.transform, "Update progress indicator", new Vector2(0, -53), new Vector2(260, 40), 27);
            busyMark.color = new Color(0.17f, 0.85f, 0.62f);
            primary = MakeButton(surface.transform, "Update action", new Vector2(-190, -125), new Color(0.17f, 0.85f, 0.62f), out actionText);
            actionText.color = new Color(0.025f, 0.09f, 0.07f);
            actionText.text = "Please wait";
            primary.onClick.AddListener(Act);
            TextMeshProUGUI laterText;
            later = MakeButton(surface.transform, "Later", new Vector2(220, -125), new Color(0.16f, 0.21f, 0.25f), out laterText);
            laterText.text = "Later";
            later.onClick.AddListener(() => { var client = VrUpdateClient.Instance; dismissedVersion = client?.Release?.VersionCode ?? 0; surface.SetActive(false); });
#if KLEVER_DPN
            Klever.DPVR.DpvrUpdatePopupInput.Configure(panel, primary, later);
#else
            BindQuestPointer(surface, rect);
#endif
            surface.SetActive(false);
        }
#if !KLEVER_DPN
        private RectTransform questMarker;
        private UnityEngine.UI.Outline primaryOutline, laterOutline;
        private Oculus.Interaction.RayInteractable questTarget;
        private Oculus.Interaction.RayInteractor[] questRays;

        private void BindQuestPointer(GameObject surface, RectTransform rect)
        {
            var collider = surface.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(rect.rect.width, rect.rect.height, 2);
            var hitSurface = surface.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            hitSurface.InjectAllColliderSurface(collider);
            questTarget = surface.AddComponent<Oculus.Interaction.RayInteractable>();
            questTarget.InjectAllRayInteractable(hitSurface);
            questTarget.WhenPointerEventRaised += evt =>
            {
                if (evt.Type != Oculus.Interaction.PointerEventType.Select) return;
                var button = QuestButtonAt(evt.Pose.position);
                if (button != null && button.IsInteractable()) button.onClick.Invoke();
            };
            primaryOutline = Outline(primary.gameObject, new Color(0.65f, 1f, 0.88f));
            laterOutline = Outline(later.gameObject, new Color(0.65f, 1f, 0.88f));
            var go = new GameObject("Quest update pointer", typeof(RectTransform), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(rect, false);
            questMarker = (RectTransform)go.transform;
            questMarker.sizeDelta = new Vector2(14, 14);
            var image = go.GetComponent<Image>();
            image.material = overlay;
            image.raycastTarget = false;
            Outline(go, new Color(0.04f, 0.06f, 0.07f)).enabled = true;
            go.SetActive(false);
        }

        private static UnityEngine.UI.Outline Outline(GameObject go, Color color)
        {
            var border = go.AddComponent<UnityEngine.UI.Outline>();
            border.effectColor = color;
            border.effectDistance = new Vector2(4, -4);
            border.enabled = false;
            return border;
        }

        private Button QuestButtonAt(Vector3 point)
        {
            var actionRect = (RectTransform)primary.transform;
            if (actionRect.rect.Contains(actionRect.InverseTransformPoint(point))) return primary;
            var laterRect = (RectTransform)later.transform;
            return laterRect.rect.Contains(laterRect.InverseTransformPoint(point)) ? later : null;
        }

        private void UpdateQuestPointer(bool busy)
        {
            primaryOutline.enabled = laterOutline.enabled = false;
            questMarker.gameObject.SetActive(false);
            if (questRays == null || questRays.Length == 0) return;
            foreach (var ray in questRays)
            {
                if (ray == null || !ray.isActiveAndEnabled || ray.Interactable != questTarget ||
                    !ray.CollisionInfo.HasValue) continue;
                var point = ray.CollisionInfo.Value.Point;
                var local = panel.transform.InverseTransformPoint(point);
                questMarker.localPosition = new Vector3(local.x, local.y, -7);
                questMarker.localRotation = Quaternion.Euler(0, 0, busy ? Time.unscaledTime * 240f : 45f);
                questMarker.gameObject.SetActive(true);
                var button = QuestButtonAt(point);
                primaryOutline.enabled = button == primary && primary.IsInteractable();
                laterOutline.enabled = button == later && later.IsInteractable();
                break;
            }
        }
#endif

        private TextMeshProUGUI Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.layer = 5; go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize;
            text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.richText = false;
            text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
            // TMP's UI shader reads unity_GUIZTestMode, not _ZTestMode. Use a
            // private material so this overlay cannot alter classroom text.
            var material = new Material(text.fontSharedMaterial);
            material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            material.renderQueue = 5000;
            text.fontSharedMaterial = material;
            textMaterials.Add(material);
            return text;
        }
        private Button MakeButton(Transform parent, string name, Vector2 position, Color color, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.layer = 5; go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(350, 70); rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.color = Color.white; image.material = overlay;
            label = Label(go.transform, name + " label", Vector2.zero, new Vector2(335, 65), 30);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.55f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
            button.colors = colors;
            return button;
        }
        private void Act()
        {
            var client = VrUpdateClient.Instance;
            if (client == null) return;
            if (client.State == VrUpdateClient.UpdateState.Available) client.RequestDownload();
            else if (client.State == VrUpdateClient.UpdateState.Unavailable) client.RetryCheck();
            else client.RequestInstall();
        }
        private void LateUpdate()
        {
            var client = VrUpdateClient.Instance;
            if (client == null || client.Release == null || !VrUpdateClient.SafeToDownload() || dismissedVersion == client.Release.VersionCode) { panel.gameObject.SetActive(false); return; }
            var state = client.State;
            bool visible = state == VrUpdateClient.UpdateState.Available || state == VrUpdateClient.UpdateState.Downloading || state == VrUpdateClient.UpdateState.Verifying ||
                state == VrUpdateClient.UpdateState.VerifiedAwaitingInstaller || state == VrUpdateClient.UpdateState.PermissionRequired || state == VrUpdateClient.UpdateState.Unavailable || client.InstallationActive;
            if (!visible) { panel.gameObject.SetActive(false); return; }
            var currentEye = Camera.main;
            if (currentEye == null) { panel.gameObject.SetActive(false); return; }
            if (!panel.gameObject.activeSelf || eye != currentEye)
            {
                eye = currentEye;
                PlaceInFront();
                panel.gameObject.SetActive(true);
#if !KLEVER_DPN
                questRays = FindObjectsOfType<Oculus.Interaction.RayInteractor>(true);
#endif
            }
            panel.worldCamera = eye;
            heading.text = "Klever update · " + client.Release.VersionName;
            bool busy = state == VrUpdateClient.UpdateState.Downloading ||
                        state == VrUpdateClient.UpdateState.Verifying || client.InstallationActive;
            busyMark.gameObject.SetActive(busy);
            if (busy)
            {
                int phase = Mathf.FloorToInt(Time.unscaledTime * 4f) % 3;
                busyMark.text = "Klever " + new string('·', phase + 1);
            }
            primary.interactable = false;
            later.interactable = !client.InstallationActive;
            actionText.text = "Please wait";
            if (state == VrUpdateClient.UpdateState.Available) { detail.text = "A new app update is available. Your lessons and saved results will be kept."; actionText.text = "Download update"; primary.interactable = true; }
            else if (state == VrUpdateClient.UpdateState.Downloading)
            {
                var rate = client.DownloadBytesPerSecond;
                var speed = rate < 1 ? "Measuring speed…" : rate >= 1024 * 1024 ? (rate / (1024 * 1024)).ToString("0.0") + " MB/s" : (rate / 1024).ToString("0") + " KB/s";
                var remaining = client.SecondsRemaining;
                var estimate = remaining < 0 ? "" : remaining < 60 ? " · less than a minute left" : " · about " + System.Math.Ceiling(remaining / 60d).ToString("0") + " min left";
                detail.text = "Downloading update · " + Mathf.Clamp(Mathf.RoundToInt(client.DownloadProgress * 100), 0, 100) + "%\n" + speed + estimate + "\nKeep the headset awake until it finishes.";
            }
            else if (state == VrUpdateClient.UpdateState.Verifying) detail.text = "Checking the downloaded update…";
            else if (state == VrUpdateClient.UpdateState.Unavailable) { detail.text = "The update could not be completed. Your current app is available. Check again when connected."; actionText.text = "Check again"; primary.interactable = true; }
            else if (state == VrUpdateClient.UpdateState.PermissionRequired) { detail.text = "Android needs one-time permission to install updates from Klever. Allow it, then return here."; actionText.text = "Continue setup"; primary.interactable = true; }
            else if (state == VrUpdateClient.UpdateState.VerifiedAwaitingInstaller)
            {
                bool pending = client.IsDeliveryPending;
                if (!pending && autoInstallerAttempt != client.Release.VersionCode)
                {
                    autoInstallerAttempt = client.Release.VersionCode;
                    if (client.TryOpenInstallerAutomatically()) return;
                }
                detail.text = pending ? "Finishing result delivery. Installation will be available when your saved results are confirmed." : "Update verified. Android may ask you to confirm installation. Klever will reopen afterward if your headset permits it.";
                actionText.text = pending ? "Saving results…" : "Install update"; primary.interactable = !pending;
            }
            else detail.text = state == VrUpdateClient.UpdateState.Installing ? "Preparing the Android installer. Please wait…" : "Complete the Android installation prompt. If you cancel, your current app stays available.";
#if !KLEVER_DPN
            UpdateQuestPointer(busy);
#endif
        }
        private void PlaceInFront()
        {
            var forward = Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(eye.transform.right, Vector3.up);
            forward.Normalize();
            panel.transform.SetPositionAndRotation(eye.transform.position + forward * 1.8f, Quaternion.LookRotation(forward, Vector3.up));

        }
        private void OnApplicationPause(bool paused) { if (!paused && panel != null) panel.gameObject.SetActive(false); }
        private void OnDestroy()
        {
            if (overlay != null) Destroy(overlay);
            foreach (var material in textMaterials) if (material != null) Destroy(material);
        }
    }
}
