using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Klever.VR
{
    // Independent of scene loading/lesson objects so feedback stays visible
    // when the authored loading screen temporarily hides the classroom.
    public sealed class SessionPresentation : MonoBehaviour
    {
        public enum Stage { Hidden, Loading, Completed, Waiting, Paused }
        private static SessionPresentation instance;
        private Canvas panel;
        private TextMeshProUGUI label;
        private Camera eye;
        private Transform spinner;
        private Material overlayMaterial;
        private float narrationReadyAt = -1f;
        private Stage stageBeforePause;
        private string messageBeforePause;
        private bool visibleBeforePause;
        public Stage CurrentStage { get; private set; }
        public bool IsVisible => panel != null && panel.gameObject.activeSelf;
        public string Message => label != null ? label.text : string.Empty;
        public static SessionPresentation Instance => instance;

        private static SessionPresentation Ensure()
        {
            if (instance != null) return instance;
            var root = new GameObject("[DPVR Session Feedback]");
            instance = root.AddComponent<SessionPresentation>();
            if (Application.isPlaying) DontDestroyOnLoad(root);
            instance.Build();
            return instance;
        }

        private void Build()
        {
            var surface = new GameObject("Session message", typeof(RectTransform), typeof(Canvas), typeof(Image));
            surface.layer = 5;
            surface.transform.SetParent(transform, false);
            panel = surface.GetComponent<Canvas>();
            panel.renderMode = RenderMode.WorldSpace;
            panel.sortingOrder = 32767;
            panel.overrideSorting = true;
            var rect = surface.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1100, 400);
            rect.localScale = Vector3.one * 0.001f;
            var background = surface.GetComponent<Image>();
            overlayMaterial = new Material(Shader.Find("UI/Default"));
            overlayMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            overlayMaterial.renderQueue = 5000;
            background.material = overlayMaterial;
            background.color = new Color(0.025f, 0.055f, 0.09f, 0.97f);
            background.raycastTarget = false;
            var textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = 5;
            textObject.transform.SetParent(surface.transform, false);
            label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 40;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(35, 90);
            label.rectTransform.offsetMax = new Vector2(-35, -20);
            label.fontMaterial.SetFloat("_ZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            label.fontMaterial.renderQueue = 5000;
            var loader = new GameObject("Waiting indicator", typeof(RectTransform));
            loader.layer = 5;
            loader.transform.SetParent(surface.transform, false);
            spinner = loader.transform;
            ((RectTransform)spinner).anchoredPosition = new Vector2(0, -125);
            for (int i = 0; i < 12; i++)
            {
                var tick = new GameObject("Indicator segment", typeof(RectTransform), typeof(Image));
                tick.layer = 5; tick.transform.SetParent(spinner, false);
                float angle = i * Mathf.PI / 6;
                var tickRect = (RectTransform)tick.transform;
                tickRect.sizeDelta = new Vector2(7, 18);
                tickRect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 30;
                tickRect.localRotation = Quaternion.Euler(0, 0, i * 30 - 90);
                var image = tick.GetComponent<Image>(); image.raycastTarget = false;
                image.material = overlayMaterial; image.color = new Color(0.2f, 0.9f, 0.7f, 0.2f + i / 15f);
            }
            surface.SetActive(false);
        }

        public static void ShowLoading()
        {
            var view = Ensure();
            view.CurrentStage = Stage.Loading;
            view.narrationReadyAt = -1f;
            view.label.text = "<b>Preparing lesson</b>\nPlease wait…";
            view.panel.gameObject.SetActive(true);
            view.FollowView();
            Debug.Log("[VR-SESSION] Loading feedback visible.");
        }

        public static void NarrationReady()
        {
            if (instance == null || instance.CurrentStage != Stage.Loading) return;
            // Retain visible feedback through the first rendered classroom frames.
            instance.narrationReadyAt = Time.realtimeSinceStartup;
            Debug.Log("[VR-SESSION] Narration started; waiting for classroom rendering.");
        }

        public static void ShowCompleted(int correct, int total)
        {
            var view = Ensure();
            view.CurrentStage = Stage.Completed;
            // The authored green board is the single score display.
            view.label.text = "<b>Experience completed successfully</b>\nWaiting for your teacher to finish or end the session.";
            view.panel.gameObject.SetActive(true);
            view.FollowView();
            Debug.Log("[VR-SESSION] Completion feedback visible.");
        }

        public static void ShowWaiting()
        {
            var view = Ensure();
            view.CurrentStage = Stage.Waiting;
            view.label.text = "<b>Session ended</b>\nGetting ready for your next lesson…";
            view.panel.gameObject.SetActive(true);
            view.FollowView();
        }

        public static void SetPaused(bool paused)
        {
            if (paused)
            {
                var view = Ensure();
                if (view.CurrentStage == Stage.Paused) return;
                view.stageBeforePause = view.CurrentStage;
                view.messageBeforePause = view.Message;
                view.visibleBeforePause = view.IsVisible;
                view.CurrentStage = Stage.Paused;
                view.label.text = "<b>Lesson paused by your teacher</b>\nPlease wait. Your lesson will continue shortly.";
                view.panel.gameObject.SetActive(true);
                view.FollowView();
            }
            else if (instance != null && instance.CurrentStage == Stage.Paused)
            {
                instance.CurrentStage = instance.stageBeforePause;
                instance.label.text = instance.messageBeforePause;
                instance.panel.gameObject.SetActive(instance.visibleBeforePause);
            }
        }
        public static void Hide()
        {
            if (instance == null) return;
            instance.CurrentStage = Stage.Hidden;
            instance.panel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            SetPaused(LessonPause.IsPaused);
            FollowView();
            if (IsVisible && spinner != null) spinner.Rotate(0, 0, -140f * Time.unscaledDeltaTime);
            if (CurrentStage == Stage.Loading && narrationReadyAt >= 0 && Time.realtimeSinceStartup - narrationReadyAt >= 1f)
            {
                var room = ChangeClassRoom.Instance;
                if (room != null && room.classRooms != null)
                    foreach (var classroom in room.classRooms)
                        if (classroom != null && classroom.activeInHierarchy && classroom.GetComponentInChildren<Renderer>() != null) { Hide(); break; }
            }
        }
        public void FollowView()
        {
            if (!IsVisible) return;
            if (eye == null || !eye.isActiveAndEnabled) eye = Camera.main;
            if (eye == null) return;
            panel.worldCamera = eye;
            panel.transform.SetPositionAndRotation(eye.transform.position + eye.transform.forward * 1.5f + (CurrentStage == Stage.Completed ? -eye.transform.up * 0.35f : Vector3.zero),
                eye.transform.rotation);
        }

        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
