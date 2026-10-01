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
    private float duration;
    public StereoPacking stereoPackingMode = StereoPacking.TopBottom;
    private ServiceScript service;
    private string videoScript;
    private string videoSound;
    private bool videoOpened = false;
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
    private Coroutine videoCoroutine;

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

    // Main play function
    public void PlayVideo()
    {
        videoPlayerURL = PlayerPrefs.GetString(StaticStrings.videoUrl);
        Klass.Net.LiveViewPublisher.Instance?.ShowContent("video", videoPlayerURL, videoPlayerURL);

        if (string.IsNullOrEmpty(videoPlayerURL))
        {
            Debug.LogWarning("[VideoManager] No video URL found. Skipping video playback.");
            if (GameManager.Instance != null && GameManager.Instance.EnableClassAndBoard != null)
            {
                GameManager.Instance.EnableClassAndBoard.Invoke();
            }
            if (googleTextToSpeech != null) googleTextToSpeech.StopAudio();
            Performe();
            return;
        }

        if (!string.IsNullOrEmpty(youtubeURL))
        {
            // Case: YouTube URL is provided
            if (youtubeVideoStatus != "failed")
            {
                youtubeURLGeneratedEvent.Invoke();
                // Case: Backend successfully generated a downloadable YouTube URL
                Debug.Log($"[YouTube] Valid download URL found. Starting video from {startTimer}s to {endTimer}s.");
                GameManager.Instance.DisableClassAndBoard.Invoke();
                mediaPlayer.gameObject.SetActive(true);
                this.gameObject.transform.GetChild(0).gameObject.SetActive(true);
                videoScript = PlayerPrefs.GetString(StaticStrings.videoScript);
                videoSound = PlayerPrefs.GetString(StaticStrings.videoSound);
                //videoPlayer.gameObject.SetActive(true);
                //videoPlayer.source = VideoSource.Url;
                //videoPlayer.url = videoPlayerURL;
                bool isOpening = mediaPlayer.OpenMedia(new MediaPath(videoPlayerURL, MediaPathType.AbsolutePathOrURL), autoPlay: true);
                videoOpened = isOpening;
                // Use startTimer and endTimer from AssignYoutubeProperty
                StartCoroutine(OpenVideoAndSeek(startTimer, endTimer));
            }
            else
            {
                //avpro.SetActive(false);
                //ChangeSkyBox.SetSkybox(skyBox_VideoPlaye_Mat);
                //videoPlayer.gameObject.SetActive(true);
                //videoPlayer.source = VideoSource.Url;
                //videoPlayer.url = videoPlayerURL;
                //videoPlayer.Play();
                GameManager.Instance.EnableClassAndBoard.Invoke();
                // Case: Backend failed to generate a downloadable YouTube URL
                Debug.LogError("[YouTube] Download URL not generated. Please verify backend YouTube URL generation logic.");
                // Optional: trigger fallback logic
                googleTextToSpeech.StopAudio();
                Performe();
            }
        }
        else
        {
            youtubeURLGeneratedEvent.Invoke();
            mediaPlayer.gameObject.SetActive(true);
            videoScript = PlayerPrefs.GetString(StaticStrings.videoScript);
            videoSound = PlayerPrefs.GetString(StaticStrings.videoSound);
            this.gameObject.transform.GetChild(0).gameObject.SetActive(true);
            youtubePlayer.gameObject.SetActive(false);

            bool isOpening = mediaPlayer.OpenMedia(new MediaPath(videoPlayerURL, MediaPathType.AbsolutePathOrURL), autoPlay: true);
            videoOpened = isOpening;

            StartCoroutine(OpenVideo());
        }
    }

    private IEnumerator OpenVideoAndSeek(double startTime, double endTime)
    {
        // Wait until video is opened and ready
        while (!mediaPlayer.Control.IsPlaying() && !mediaPlayer.Control.IsFinished())
        {
            yield return null;
        }

        // Wait for duration to become available
        while (mediaPlayer.Info.GetDuration() <= 0)
        {
            yield return null;
        }

        double duration = mediaPlayer.Info.GetDuration();
        IsAudioNullInVideo();
        // Clamp start and end times within video duration
        startTime = Mathf.Clamp((float)startTime, 0f, (float)duration);
        endTime = Mathf.Clamp((float)endTime, 0f, (float)duration);

        // Seek to start time
        mediaPlayer.Control.Seek(startTime);
        mediaPlayer.Control.Play();

        // Automatically stop at end time
        if (endTime > startTime)
        {
            yield return new WaitUntil(() => mediaPlayer.Control.GetCurrentTime() >= endTime);
            mediaPlayer.Control.Pause();
        }
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



 IEnumerator OpenVideo()
    {
        yield return new WaitUntil(() => videoOpened);
        IsAudioNullInVideo();
    }
    public void IsAudioNullInVideo()
    {
        
        if (string.IsNullOrEmpty(videoScript)||videoSound=="mute")
        {
            //   GetMediaInfo();
            StartCoroutine(GetMediaInfo());

        }
        else
        {
           StartCoroutine(AssinOperation());
            MuteVolumn();


        }



    }

    public void Seek()
    {
        ytbPlayer.startFromSecondTime= (int)startTimer;
    }
    private IEnumerator GetMediaInfo()
    {
        // Wait until media duration becomes available
        yield return new WaitUntil(() => mediaPlayer.Info.GetDuration() > 0);

        double durationDouble = mediaPlayer.Info.GetDuration();
        duration = (float)durationDouble;

        // Case 1: YouTube video with valid start & end time
        if (!string.IsNullOrEmpty(youtubeURL) && endTimer > 0)
        {
            // Clamp values within duration
            startTimer = Mathf.Clamp(startTimer, 0f, duration);
            endTimer = Mathf.Clamp(endTimer, 0f, duration);

            // Calculate the segment playback duration
            float segmentDuration = endTimer - startTimer;

            Debug.Log($"[YouTube] Media info ready. Duration: {duration}s | Start: {startTimer}s | End: {endTimer}s | Segment: {segmentDuration}s");

            // Seek to the start time before activating next logic
            if (mediaPlayer.Control != null)
            {
                mediaPlayer.Control.Seek(startTimer);
                mediaPlayer.Control.Play();
            }

            // Wait only for the segment duration
            //yield return new WaitForSeconds(segmentDuration);
            StartCoroutine(ActivateVideoEndAction(segmentDuration));
        }
        else
        {
            // Case 2: Normal full video playback
            Debug.Log($"[Video] Media info ready. Duration: {duration}s | Playing full video...");
            //yield return new WaitForSeconds(duration);
            StartCoroutine(ActivateVideoEndAction(duration));
        }
       
    }

    private IEnumerator ActivateVideoEndAction(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (!isThreeSixty)
        {
            service.AssignTaskBasedOnCondition(GameStatus.NoAudioInVideo);
        }
        else
        {
            service.AssignTaskBasedOnCondition(GameStatus.threesixtyImage);
        }


        // VideoEndAction.Invoke();
    }
    public void Performe()
    {
        if(videoCoroutine!=null)
        {
            StopCoroutine(videoCoroutine);
            videoCoroutine = null;
        }
     
        if (GameManager.Instance.isImageSkip)
        {
            if (!GameManager.Instance.isModelSkip)
            {
                service.AssignTaskBasedOnCondition(GameStatus.SkipModel);
            }
            else
            {
                service.AssignTaskBasedOnCondition(GameStatus.WithoutSkipModel);
            }
            //service.AssignTaskBasedOnCondition(GameStatus.threesixtyImage);
        }
        else
        {
            service.AssignTaskBasedOnCondition(GameStatus.threesixtyImage);
        }

       
        // Action.Invoke();
    }
   
    private IEnumerator AssinOperation()
    {     
            googleTextToSpeech.StartTalking(videoScript);
            yield return new WaitUntil(() => googleTextToSpeech.IsAudioReady);
           videoCoroutine = StartCoroutine(IsVideoEndFirst());
            Invoke("Performe", googleTextToSpeech.audioClipLength);         
    }
    IEnumerator IsVideoEndFirst()
    {
        // Wait until video duration is available
        yield return new WaitUntil(() => mediaPlayer.Info.GetDuration() > 0);

        double durationSeconds = mediaPlayer.Info.GetDuration();
        duration = (float)durationSeconds;

        // Case 1: YouTube video with start/end times
        if (!string.IsNullOrEmpty(youtubeURL) && endTimer > 0)
        {
            // Clamp values within duration
            startTimer = Mathf.Clamp(startTimer, 0f, duration);
            endTimer = Mathf.Clamp(endTimer, 0f, duration);

            // Calculate playback time (only the part between start and end)
            float playTime = endTimer - startTimer;

            Debug.Log($"[YouTube] Video duration: {duration}s | Start: {startTimer}s | End: {endTimer}s | Play segment: {playTime}s");

            // Wait for the selected segment duration
            yield return new WaitForSeconds(playTime);
        }
        else
        {
            // Case 2: Normal full-length video
            Debug.Log($"[Video] Full duration: {duration}s | Playing entire clip...");
            yield return new WaitForSeconds(duration);
        }

        // After video ends, trigger appropriate game
        
        if (!isThreeSixty)
        {
            service.AssignTaskBasedOnCondition(GameStatus.VideoEndFirst);
        }
        else
        {
            service.AssignTaskBasedOnCondition(GameStatus.VideoEndFirst);
        }
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
        Performe();
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


   

