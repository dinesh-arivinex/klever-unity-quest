using System;
using System.Linq;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Klever.VR.Editor
{
    public static class QuestParityAudit
    {
        public static void Run()
        {
            foreach (Klever.VR.Updates.VrUpdateClient.UpdateState state in Enum.GetValues(typeof(Klever.VR.Updates.VrUpdateClient.UpdateState)))
            {
                if (Klever.VR.Updates.VrUpdateNotice.ShouldDisplay(state, 14, 0, false))
                    throw new Exception("Quest update notice interrupts an active lesson.");
            }
            foreach (var state in new[] {
                Klever.VR.Updates.VrUpdateClient.UpdateState.Downloading,
                Klever.VR.Updates.VrUpdateClient.UpdateState.Verifying,
                Klever.VR.Updates.VrUpdateClient.UpdateState.VerifiedAwaitingInstaller,
                Klever.VR.Updates.VrUpdateClient.UpdateState.PermissionRequired,
                Klever.VR.Updates.VrUpdateClient.UpdateState.Installing,
                Klever.VR.Updates.VrUpdateClient.UpdateState.AwaitingAndroid })
                if (!Klever.VR.Updates.VrUpdateNotice.ShouldDisplay(state, 14, 14, true))
                    throw new Exception("Dismissal hides active Quest update progress or installation.");
            if (Klever.VR.Updates.VrUpdateNotice.ShouldDisplay(Klever.VR.Updates.VrUpdateClient.UpdateState.Available, 14, 14, true) ||
                Klever.VR.Updates.VrUpdateNotice.ShouldDisplay(Klever.VR.Updates.VrUpdateClient.UpdateState.UpToDate, 14, 0, true))
                throw new Exception("Quest update dismissal or current-version visibility failed.");
            Debug.Log("[QUEST-UPDATE-VISIBILITY-AUDIT] PASS: active progress and installation survive dismissal; lessons remain uninterrupted.");
            var modelRoot=new GameObject("Quest placement audit"); modelRoot.AddComponent<BoxCollider>();
            var mesh=GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.transform.SetParent(modelRoot.transform,false); mesh.transform.localPosition=new Vector3(5,-3,2); mesh.transform.localScale=new Vector3(2,4,6);
            LessonModelPlacement.PlaceOnTable(modelRoot,new Vector3(-0.12f,0.65f,2.22f));
            var bounds=modelRoot.GetComponent<BoxCollider>().bounds;
            if(Mathf.Abs(bounds.min.y-0.67f)>0.001f || Mathf.Abs(bounds.center.x+0.12f)>0.001f || Mathf.Abs(bounds.center.z-2.22f)>0.001f || Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))>0.651f) throw new Exception("Quest model missed tabletop or retained an offset pivot.");
            UnityEngine.Object.DestroyImmediate(modelRoot);
            ClassroomBoardAudit.Run();
            if (VideoManager.PlaybackComplete(false, 84.79, 84.8, false, 0, 0) ||
                !VideoManager.PlaybackComplete(true, 84.8, 84.8, false, 0, 0) ||
                VideoManager.PlaybackComplete(false, 17, 20, true, 10, 18) ||
                !VideoManager.PlaybackComplete(false, 18, 20, true, 10, 18) ||
                VideoManager.PlaybackComplete(false, 20, 20, true, 10, 20))
                throw new Exception("360 playback and YouTube clip completion rules failed.");
            MqttPauseAudit.Run();
            Klever.Branding.Editor.KleverAppIcons.Apply();
            var buttonObject = new GameObject("Assessment binding regression", typeof(RectTransform), typeof(Button));
            var button = buttonObject.GetComponent<Button>();
            var wrapper = buttonObject.AddComponent<InteractableUnityEventWrapper>();
            // Newly added SDK wrappers have no serialized UnityEvent yet.
            AssessmentButtonBinding.Bind(button, 0, _ => { });
            typeof(InteractableUnityEventWrapper).GetField("_whenSelect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(wrapper, new UnityEngine.Events.UnityEvent());
            int answer = -1, calls = 0, authored = 0;
            button.onClick.AddListener(() => authored++);
            for (int i = 0; i < 3; i++)
            {
                AssessmentButtonBinding.Bind(button, i, value => { answer = value; calls++; });
                button.onClick.Invoke();
                if (answer != i || calls != i * 2 + 1 || authored != i + 1) throw new Exception("UI listener duplicated or authored listener lost.");
                wrapper.WhenSelect.Invoke();
                if (answer != i || calls != (i + 1) * 2) throw new Exception("Oculus select listener duplicated.");
            }
            AssessmentButtonBinding.Bind(null, 0, _ => { throw new Exception("Null button invoked callback."); });
            UnityEngine.Object.DestroyImmediate(buttonObject);
            var data = ContentCompatibility.Parse("{\"assessment\":[{\"typeOfGame\":\"MCQ\"}],\"videoDetail\":{\"videoURL\":\"https://example.test/current.mp4\"}}");
            if (ContentCompatibility.Assessments(data).Count != 1 || ContentCompatibility.VideoUrl(data) != "https://example.test/current.mp4") throw new Exception("Current content schema failed.");
            var legacy = ContentCompatibility.Parse("{\"assessments\":[{}],\"VideoId\":{\"videoURL\":\"https://example.test/legacy.mp4\"}}");
            if (ContentCompatibility.Assessments(legacy).Count != 1 || ContentCompatibility.VideoUrl(legacy) != "https://example.test/legacy.mp4") throw new Exception("Legacy content schema failed.");
            EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity", OpenSceneMode.Single);
            var game = UnityEngine.Object.FindObjectsOfType<GameManager>(true).Single();
            var manager = UnityEngine.Object.FindObjectsOfType<GamificationManager>(true).Single();
            GameManager.Instance = game;
            var video = UnityEngine.Object.FindObjectsOfType<VideoManager>(true).Single();
            if (video.mediaPlayer == null) throw new Exception("Scene video player missing.");
            video.mediaPlayer.AudioVolume = 0f;
            video.mediaPlayer.AudioMuted = true;
            video.RestoreVideoAudio();
            if (video.mediaPlayer.AudioVolume != 1f || video.mediaPlayer.AudioMuted) throw new Exception("A narrated clip left the next clip muted.");
            var sourceFields = new[] { "youtubeURL", "youtubeVideoStatus" };
            foreach (var field in sourceFields)
                typeof(VideoManager).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(video, "previous-source");
            video.ConfigureDirectVideoSource();
            foreach (var field in sourceFields)
                if (typeof(VideoManager).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(video) != null) throw new Exception("Direct video retained YouTube state.");
            manager.disableObjects = new GameObject[] { null, new GameObject("Optional lesson visual") };
            SessionPresentation.ShowLoading();
            if (!SessionPresentation.Instance.IsVisible) throw new Exception("Preparing feedback missing.");
            SessionPresentation.NarrationReady();
            if (!SessionPresentation.Instance.IsVisible) throw new Exception("Preparing feedback disappeared before classroom rendering.");
            manager.PrepareCompletionScreen();
            if (!DeviceSynHandler.isGameOver || !game.deviceSynHandler.isCompletedDevice || game.deviceSynHandler.CurrentPlayState != "completed") throw new Exception("Authoritative completion state failed.");
            if (!SessionPresentation.Instance.IsVisible || SessionPresentation.Instance.CurrentStage != SessionPresentation.Stage.Completed) throw new Exception("Completion feedback missing.");
            game.deviceSynHandler.PublishDownloadProgress(0.2f);
            if (game.deviceSynHandler.CurrentPlayState != "completed") throw new Exception("Progress overwrote completion.");
            SessionPresentation.NarrationReady();
            if (!SessionPresentation.Instance.IsVisible) throw new Exception("Late narration hid completion.");
            if (SessionPresentation.Instance.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget)) throw new Exception("Feedback intercepts controller rays.");
            SessionPresentation.ShowLoading();
            if (SessionPresentation.Instance.CurrentStage != SessionPresentation.Stage.Loading) throw new Exception("Next lesson retained completion.");
            GameManager.Instance = null;
            DeviceSynHandler.isGameOver = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log("[QUEST-PARITY-AUDIT] PASS: Oculus/UI rebinding, content schemas, null-safe completion, retained state priority and non-blocking session feedback. No scores submitted.");
        }
    }
}
