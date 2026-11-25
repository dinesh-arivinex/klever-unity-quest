using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;

// Native video and WebViews do not obey Unity's time scale. Capture only
// resources we actually paused, so Resume never starts deliberately idle media.
public sealed class LessonPause : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    private readonly HashSet<GameObject> objects = new HashSet<GameObject>();
    private readonly HashSet<XRBaseInteractor> interactors = new HashSet<XRBaseInteractor>();
    private readonly HashSet<VideoPlayer> videos = new HashSet<VideoPlayer>();
    private readonly HashSet<AudioSource> audio = new HashSet<AudioSource>();
    private readonly HashSet<RenderHeads.Media.AVProVideo.MediaPlayer> players = new HashSet<RenderHeads.Media.AVProVideo.MediaPlayer>();
#if UNITY_ANDROID && !UNITY_EDITOR
    private readonly HashSet<Vuplex.WebView.AndroidWebView> webViews = new HashSet<Vuplex.WebView.AndroidWebView>();
#endif
    public void Apply(bool paused, GameObject[] authoredInteractors)
    {
        IsPaused = paused;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Klever.VR.SessionPresentation.SetPaused(paused);
        if (paused)
        {
            if (authoredInteractors != null)
                foreach (var item in authoredInteractors)
                    if (item != null && item.activeSelf) { objects.Add(item); item.SetActive(false); }
            Freeze();
            return;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        foreach (var item in objects) if (item != null) item.SetActive(true);
        foreach (var item in interactors) if (item != null) item.enabled = true;
        foreach (var item in videos) if (item != null && item.isPaused) item.Play();
        foreach (var item in audio) if (item != null) item.UnPause();
        foreach (var item in players) if (item != null && item.Control != null && item.Control.IsPaused()) item.Control.Play();
#if UNITY_ANDROID && !UNITY_EDITOR
        foreach (var item in webViews) if (item != null) item.Resume();
        webViews.Clear();
#endif
        objects.Clear(); interactors.Clear(); videos.Clear(); audio.Clear(); players.Clear();
    }
    private void LateUpdate() { if (IsPaused) Freeze(); }
    private void Freeze()
    {
        Time.timeScale = 0f;
        // Pause sources before AudioListener.pause masks their playback state.
        foreach (var item in FindObjectsOfType<AudioSource>())
            if (item.isPlaying) { audio.Add(item); item.Pause(); }
        AudioListener.pause = true;
        foreach (var item in FindObjectsOfType<XRBaseInteractor>())
            if (item.enabled) { interactors.Add(item); item.enabled = false; }
        foreach (var item in FindObjectsOfType<VideoPlayer>())
            if (item.isPlaying) { videos.Add(item); item.Pause(); }
        foreach (var item in FindObjectsOfType<RenderHeads.Media.AVProVideo.MediaPlayer>())
            if (item.Control != null && item.Control.IsPlaying()) { players.Add(item); item.Control.Pause(); }
#if UNITY_ANDROID && !UNITY_EDITOR
        foreach (var prefab in FindObjectsOfType<Vuplex.WebView.CanvasWebViewPrefab>())
            if (prefab.WebView is Vuplex.WebView.AndroidWebView web && web.IsInitialized && webViews.Add(web)) web.Pause();
#endif
    }
    private void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
