using RenderHeads.Media.AVProVideo;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Video;
using LightShaft.Scripts;
using SimpleJSON;
using UnityEngine.Networking;
using TriLibCore.Fbx;

public class VideoManager : MonoBehaviour
{

   // public VideoPlayer videoPlayer;
    public MediaPlayer mediaPlayer;
    public VideoPlayer videoPlayer;
    public Material skyBox_VideoPlaye_Mat;
    public string videoPlayerURL;
    // public UnityEvent Action;
    public GoogleTextToSpeech googleTextToSpeech;
    //  public UnityEvent VideoEndAction;
    public StereoPacking stereoPackingMode = StereoPacking.TopBottom;
    private ServiceScript service;
    private string videoScript;
    private string videoSound;
    private string url;
   
    private float startTimer;
    private float endTimer;
    public GameObject youtubePlayer;
    public YoutubePlayer ytbPlayer;
    private string youtubeURL;
    private string youtubeDownloadURL;
    private string youtubeVideoStatus;
    private bool isYouTubeVideoAudio;
    //private VideoPlayer videoPlayer;
   private string YoutubeScript;
    public AudioSource YoutubeAudioSource;
    public GameObject avpro;
    private bool isThreeSixty;
    private bool playbackActive;
    private bool seekPending;
    private bool narrationPending;
    private bool playbackFinished;
    private bool playbackFailed;
    private bool subscribed;

    public UnityEvent youtubeURLGeneratedEvent;
    public static VideoManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
        service = GameManager.Instance.serviceScript;

    }

    private void Update()
    {
        if (playbackActive && !LessonPause.IsPaused) UpdatePlayback();
        if (mediaPlayer != null && mediaPlayer.gameObject.activeInHierarchy && mediaPlayer.Control != null && mediaPlayer.Info != null)
        {
            Klass.Net.LiveViewPublisher.Instance?.PublishPlayback(
                mediaPlayer.Control.GetCurrentTime(), mediaPlayer.Info.GetDuration(), mediaPlayer.Control.IsPlaying());
        }
        else if (videoPlayer != null && videoPlayer.gameObject.activeInHierarchy && videoPlayer.isPrepared)
        {
            Klass.Net.LiveViewPublisher.Instance?.PublishPlayback(videoPlayer.time, videoPlayer.length, videoPlayer.isPlaying);
        }
    }
   
    public void MuteVolumn()
    {

        mediaPlayer.AudioVolume = 0;

    }
    public void ThreeSixtyImagestatus(bool value)
    {
        isThreeSixty = value;
    }
    public void ChangeVideoType(string videoType)
    {
        MediaHints hints = mediaPlayer.FallbackMediaHints;
        hints.stereoPacking = videoType == "stereoscopic-side-to-side" ? StereoPacking.LeftRight :
            videoType == "stereoscopic-top-to-bottom" ? StereoPacking.TopBottom : StereoPacking.None;
        mediaPlayer.FallbackMediaHints = hints;
        /*  if (videoType == "stereoscopic-side-to-side")
          {
              MediaHints hints = mediaPlayer.FallbackMediaHints;
              hints.stereoPacking = StereoPacking.LeftRight; // Set for left-right stereoscopic video
              mediaPlayer.FallbackMediaHints = hints;
          }
          else if (videoType == "stereoscopic-top-to-bottom")
          {
              MediaHints hints = mediaPlayer.FallbackMediaHints;
              hints.stereoPacking = stereoPackingMode;
              mediaPlayer.FallbackMediaHints = hints;
          }
          else
          {
              DebugUtil.Log("Playing Properly");
          }*/
    }

    public void PlayVideo()
    {
        playbackActive = false;
        mediaPlayer.CloseMedia();
        RestoreVideoAudio();
        if (!subscribed)
        {
            mediaPlayer.Events.AddListener(OnPlaybackEvent);
            subscribed = true;
        }

        videoPlayerURL = PlayerPrefs.GetString(StaticStrings.videoUrl);
        videoScript = PlayerPrefs.GetString(StaticStrings.videoScript);
        videoSound = PlayerPrefs.GetString(StaticStrings.videoSound);
        bool isYouTube = !string.IsNullOrEmpty(youtubeURL);
        playbackFailed = string.IsNullOrWhiteSpace(videoPlayerURL) || (isYouTube && youtubeVideoStatus == "failed");
        playbackActive = true;
        playbackFinished = false;
        seekPending = isYouTube && startTimer > 0f;
        narrationPending = !string.IsNullOrEmpty(videoScript) && videoSound != "mute";
        if (playbackFailed) return;

        Klass.Net.LiveViewPublisher.Instance?.ShowContent("video", videoPlayerURL, videoPlayerURL);
        youtubeURLGeneratedEvent.Invoke();
        if (isYouTube) GameManager.Instance.DisableClassAndBoard.Invoke();
        mediaPlayer.gameObject.SetActive(true);
        transform.GetChild(0).gameObject.SetActive(true);
        youtubePlayer.SetActive(false);
        mediaPlayer.Loop = false;
        if (narrationPending) MuteVolumn();
        if (!mediaPlayer.OpenMedia(new MediaPath(videoPlayerURL, MediaPathType.AbsolutePathOrURL), autoPlay: false))
            playbackFailed = true;
        waitingToStart = true;
    }

    private bool waitingToStart;

    private void UpdatePlayback()
    {
        if (playbackFailed)
        {
            FailPlayback();
            return;
        }

        var control = mediaPlayer.Control;
        var info = mediaPlayer.Info;
        if (control == null || info == null) return;
        if (waitingToStart && control.CanPlay() && (!seekPending || info.GetDuration() > 0))
        {
            waitingToStart = false;
            if (seekPending && startTimer < info.GetDuration())
                control.Seek(startTimer);
            control.Play();
            return;
        }
        if (waitingToStart) return;
        if (narrationPending && control.IsPlaying())
        {
            narrationPending = false;
            googleTextToSpeech.StartTalking(videoScript);
        }
        if (!PlaybackComplete(playbackFinished || control.IsFinished(), control.GetCurrentTime(),
                info.GetDuration(), !string.IsNullOrEmpty(youtubeURL), startTimer, endTimer)) return;

        Debug.Log($"[QUEST-VIDEO] Playback completed at {control.GetCurrentTime():F2}/{info.GetDuration():F2}s.");
        playbackActive = false;
        mediaPlayer.CloseMedia();
        googleTextToSpeech.StopAudio();
        Klass.Net.LiveViewPublisher.Instance?.CompleteContent("video");
        service.AssignTaskBasedOnCondition(GameStatus.threesixtyImage);
    }

    public static bool PlaybackComplete(bool finished, double time, double duration, bool isYouTube, double start, double end)
    {
        return finished || (isYouTube && duration > 0 && end > start && end < duration && time >= end);
    }

    private void OnPlaybackEvent(MediaPlayer player, MediaPlayerEvent.EventType eventType, ErrorCode error)
    {
        if (!playbackActive) return;
        if (eventType == MediaPlayerEvent.EventType.FinishedPlaying) playbackFinished = true;
        if (eventType == MediaPlayerEvent.EventType.Error) playbackFailed = true;
    }

    private void OnDestroy()
    {
        if (subscribed && mediaPlayer != null) mediaPlayer.Events.RemoveListener(OnPlaybackEvent);
    }

    public void FailPlayback()
    {
        if (!playbackActive) return;
        playbackActive = false;
        mediaPlayer.CloseMedia();
        googleTextToSpeech.StopAudio();
        Klass.Net.LiveViewPublisher.Instance?.HideContent("video-playback-error");
        GameManager.Instance.EnableClassAndBoard.Invoke();
        GameManager.Instance.deviceSynHandler?.PublishDownloadError("The headset could not play the lesson video.");
        service.AssignTaskBasedOnCondition(GameStatus.threesixtyImage);
    }

    public void AssignYoutubeProperty(string youTubeStartTimer, string youTubeEndTimer, bool youTubeVideoAudio, string youtubeUrl, string youtubeDownloadableUrl, string script, string status)
    {
        YoutubeScript = script;
        isYouTubeVideoAudio = youTubeVideoAudio;
        youtubeURL = youtubeUrl;
        youtubeDownloadURL = youtubeDownloadableUrl;
        youtubeVideoStatus = status;
        // Parse Start Time
        if (!string.IsNullOrEmpty(youTubeStartTimer))
        {
            string[] startTimeParts = youTubeStartTimer.Split(':');
            int startMinutes = int.Parse(startTimeParts[0]);
            int startSeconds = int.Parse(startTimeParts[1]);
            startTimer = startMinutes * 60 + startSeconds;
        }
        else
        {
            startTimer = 0; // Default to start of video
        }

        // Parse End Time
        if (!string.IsNullOrEmpty(youTubeEndTimer))
        {
            string[] endTimeParts = youTubeEndTimer.Split(':');
            int endMinutes = int.Parse(endTimeParts[0]);
            int endSeconds = int.Parse(endTimeParts[1]);
            endTimer = endMinutes * 60 + endSeconds;
        }
        else
        {
            endTimer = 0; // 0 means no stop limit
        }
    }


    // Event triggered
    public void YoutubeError()
    {

        StartCoroutine(PostRequest("https://dummyjson.com/auth", "Error Occure"));
    }
    IEnumerator PostRequest(string api, string jsonData)
    {

        var req = new UnityWebRequest(api, "POST");
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
        req.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);

        req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        //  req.certificateHandler = new BypassCertificateHandler();
        //Send the request then wait here until it returns
        yield return req.SendWebRequest();
        if (req.isNetworkError) // error in request
        {
            DebugUtil.Log("Error While Sending: " + req.error);
        }
        else // done
        {

            DebugUtil.Log("return" + req.downloadHandler.text);
            string value = req.downloadHandler.text;
            //  Employee employee= JsonUtility.FromJson<Employee>(value);
            //    DebugUtil.Log(employee.username);   

        }

    }



    public void RestoreVideoAudio()
    {
        if (mediaPlayer == null) return;
        mediaPlayer.AudioVolume = 1f;
        mediaPlayer.AudioMuted = false;
    }

    public void ConfigureDirectVideoSource()
    {
        youtubeURL = null;
        youtubeVideoStatus = null;
        startTimer = 0f;
        endTimer = 0f;
    }

    public void Seek()
    {
        ytbPlayer.startFromSecondTime= (int)startTimer;
    }
    void OnPrepareCompleted(VideoPlayer source)
    {
        Seek();


        if (isYouTubeVideoAudio)
        {
          
            YoutubeAudioSource.mute = true;
            googleTextToSpeech.StartTalking(YoutubeScript);
        }
        float length = endTimer - startTimer;
        StartCoroutine(StopVideoAfterDuration(youtubePlayer.GetComponent<VideoPlayer>(), length));
           
    }
   
   
    IEnumerator StopVideoAfterDuration(VideoPlayer videoPlayer, double duration)
    {
        yield return new WaitForSeconds((float)duration);

        // Pause or stop the video
        videoPlayer.Pause(); // or videoPlayer.Stop() if you want to stop it completely
        youtubePlayer.SetActive(false);
        service.AssignTaskBasedOnCondition(GameStatus.threesixtyImage);
    }
    public class ChangeSkyBox
    {
        public static Material[] skyBoxMaterialNew
        {
            get
            {
                if (ChangeClassRoom.Instance == null) return null;
                return ChangeClassRoom.Instance.skyBoxMaterials;
            }
        }

        public static void SetSkybox(Material newSkybox)
        {
            RenderSettings.skybox = newSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }

}// Add this line to link your error handler in the Inspector


   

