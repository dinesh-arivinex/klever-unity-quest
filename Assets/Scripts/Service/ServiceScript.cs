
using System.Collections;
using System.Collections.Generic;
using TriLibCore;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Events;
using static GoogleTextToSpeech;

public enum GameStatus
{
    SkipVideo,
    SkipModel,
    WithoutSkipVideo,
    WithoutSkipModel,
    SkipVideoAndModel,
    VideoEndFirst,
    NormalMode,
    Mcq,
    NoAudioInVideo,
    DisbleModelScript,
    AssignVideoStatusAsSkip,
    AssignVideoStatusAsNotSkip,
    AssessmentsNull,
    threesixtyImage,
    threesixtyImageExist,
        skipThreeSixty

}
public class ServiceScript : MonoBehaviour
{
    [Header("total model")]
    private int downloadedModel = 0;
    public GameObject[] loadModel;
    private bool isThreeSixty;

    [Header("Script Initialization")]
    private AnnotationManager annotationManager;
    private RuntimeImportBehaviour runtimeImport;
    public  GoogleTextToSpeech googleTextToSpeech;
    public VideoManager videoManager;
    private AssetDownloader assetDownloader;
    private API api;
    private ClassVrManager classVrManager;
    private DeviceSynHandler deviceSynHandler;
    private WebManager webManager;

    private RuntimeImportBehaviourHelper runtimeImportBehaviourHelper;
    private ThreesixtyImageManager threesixtyImageManager;
    public LoadingBar loadingBar;
    
    [Header("Script Initialization")]
    public UnityEvent WithoutSkipingVideo;
    public UnityEvent WithoutSkippingModel;
    public UnityEvent VideoEndFirst;
    public UnityEvent MCQ;

    public GameStatus statusOfGame;
   

    // Start is called before the first frame update
    void Start()
    {
        FindReferance();
       
    }

    public void FindReferance()
    {
        webManager= GameManager.Instance.webManager;
        deviceSynHandler = GameManager.Instance.deviceSynHandler;
        api = GameManager.Instance.api;
       // loadingBar =FindAnyObjectByType<LoadingBar>();
        annotationManager = GameManager.Instance.annotationManager;
       
        runtimeImport = FindObjectOfType<RuntimeImportBehaviour>();
       // videoManager = FindObjectOfType<VideoManager>();
        assetDownloader= GameManager.Instance.assetDownloader;
        runtimeImportBehaviourHelper =FindObjectOfType<RuntimeImportBehaviourHelper>();
        classVrManager = GameManager.Instance.vrManager;
        threesixtyImageManager = GameManager.Instance.threesixtyImageManager;
    }
    public void AssignAnnotation(List<string> modelCoordinatesJson,List<int>order)
    {
      
        annotationManager.AssignValueTOJson(modelCoordinatesJson, order);
        int urlCount= api.allUrl.Count;
        UrlCounter(urlCount);
       
    }
    public void UrlCounter(int count)
    {
        for (downloadedModel = 0; downloadedModel < count; downloadedModel++)
        {
            if (loadModel != null && downloadedModel < loadModel.Length && loadModel[downloadedModel] != null)
            {
                var behaviour = loadModel[downloadedModel].GetComponent<RuntimeImportBehaviour>();
                string url = api.allUrl[downloadedModel];
                if (behaviour != null)
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        behaviour.StartDownloadingModel(url.Trim());
                    }
                    else
                    {
                        behaviour.SkipDownload();
                    }
                }
            }
        }
    }
    
    public void AssignTaskBasedOnCondition(GameStatus currentStatus)
    {
        GameManager.Instance.assetDownloaderObj.SetActive(true);
        GameManager.Instance.simulationManagerObj.SetActive(true);
        GameManager.Instance.videoManagerObj.SetActive(true);
        GameManager.Instance._360ImageExperienceObj.SetActive(true); 
        GameManager.Instance.gamificationManagerObj.SetActive(true);
        switch (currentStatus)
        {
            case GameStatus.SkipVideo:
                SkipVideos();
                break;
            case GameStatus.SkipModel:
                SkipModelDownload();
                break;
            case GameStatus.SkipVideoAndModel:
                SkipVideoAndModel();
                break;
            case GameStatus.WithoutSkipVideo:
                WithoutSkipVideo();
                break;
            case GameStatus.WithoutSkipModel:
                WithoutSkipModel();
                break;
            case GameStatus.VideoEndFirst:
                VideoEndingFirst();
                break;
            case GameStatus.Mcq:
                StartMCQ();
                break;
            case GameStatus.NoAudioInVideo:
                threesixtyImageManager.ThreeSixtyExperianceStarted();
                //WithoutSkipModel(); //Uncomment
                break;
            case GameStatus.DisbleModelScript:
               StartCoroutine(DisableModelScript());
                break;
            case GameStatus.AssignVideoStatusAsSkip:
                AssignVideoStatusAsSkip();
                break;
            case GameStatus.AssignVideoStatusAsNotSkip:
                AssignVideoStatusAsNotSkip();
                break;
            case GameStatus.AssessmentsNull:
                AssessnetNullManager();
                break;
            case GameStatus.threesixtyImage:
                ThreesixtyVideoExperiance();
                break;
            case GameStatus.threesixtyImageExist:
                ThreesixtyVideoExperianceExistnce();
                break;
           


            default:
                print("Incorrect intelligence level.");
                break;
        }


        

    }
    public void ThreesixtyVideoExperianceExistnce()
    {
        isThreeSixty = true;
    }
    public void ThreesixtyVideoExperiance()
    {
        threesixtyImageManager.ThreeSixtyExperianceStarted();
    }
  
    public void AssessnetNullManager()
    {
        assetDownloader.AssignMCQStatus(false);
    }
    public void SkipVideos()
    {
        // WithoutSkippingModel.Invoke();
        threesixtyImageManager.ThreeSixtyExperianceStarted();
    }
    public void SkipModelDownload()
    {
       
        WithoutSkippingModel.Invoke();
        // StartMCQ();
        webManager.StartSimulation();
       // ThreesixtyVideoExperiance();
       //



    }
    public void SkipVideoAndModel()
    {
        AssignTaskBasedOnCondition(GameStatus.DisbleModelScript);
       googleTextToSpeech.InitialVideoStatus(GameStatus.threesixtyImage);
        //ThreesixtyVideoExperiance();
    }
    public void WithoutSkipVideo()
    {
        WithoutSkipingVideo.Invoke();
        
        // videoManager.ChangeVideoType();
    }
    public void VideoEndingFirst()
    {
        VideoEndFirst.Invoke();
    }
    public void WithoutSkipModel()
    {
        string modelUrl = PlayerPrefs.GetString(StaticStrings.modelUrl);
        if (!string.IsNullOrEmpty(modelUrl))
        {
            var details = new SimpleJSON.JSONObject();
            details["name"] = PlayerPrefs.GetString(StaticStrings.modelName);
            Klass.Net.LiveViewPublisher.Instance?.ShowContent("3dmodel", modelUrl, modelUrl, details);
        }

       // assetDownloader.timeRemaining = 0;
        WithoutSkippingModel.Invoke();
    }
    public void StartMCQ()
    {
       
        MCQ.Invoke();
    }

    IEnumerator DisableModelScript()
    {
        
        if (runtimeImportBehaviourHelper!=null)
        {
            if (runtimeImport != null)
            {
                runtimeImport.enabled = false;
            }
            yield return new WaitUntil(() => deviceSynHandler.isOpening);
            runtimeImportBehaviourHelper.CustomStartSpeak();
            videoManager.ThreeSixtyImagestatus(isThreeSixty);
        }
        else
        {
            videoManager.ThreeSixtyImagestatus(isThreeSixty);
            loadingBar.CustomSumulatedLoading();
            yield return new WaitUntil(() => deviceSynHandler.isOpening);
            StartCoroutine(StartTalkingDelay());
         

        }

      
    }
   
   IEnumerator StartTalkingDelay()
    {
        yield return new WaitForSeconds(3);
        googleTextToSpeech.AssistantStartTalking();
    }
    public void AssignVideoStatusAsSkip()
    {
        googleTextToSpeech.InitialVideoStatus(GameStatus.SkipVideo);
    }
    public void AssignVideoStatusAsNotSkip()
    {
        googleTextToSpeech.InitialVideoStatus(GameStatus.WithoutSkipVideo);
    }
    public void QueryApiAndPassResult(string gender)
    {
        if(gender=="male")
        {
            googleTextToSpeech.SetVoiceNameAndGender(GendeR.MALE);
        }
        else
        {
            googleTextToSpeech.SetVoiceNameAndGender(GendeR.FEMALE);
        }
       
      
    }
   
}
