
using GLTFast.Schema;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

using UnityEngine.Video;


public class API : MonoBehaviour
{
    [System.Serializable]
    public class ApiResponse
    {
        public int code;
        public string message;
        // Include other fields as necessary
    }
    [HideInInspector]
    public string assessmentsString;
    public GameObject boardParent;
    public UnityEvent immediatActionApiDownload;
    private ServiceScript serviceScript;
    private JSONNode modelDetail;
    private string teacherCharacterGender;
    private bool isModelDownload;
    private bool isPlayingVideo;
    private JSONArray modelDetails;
    private int currentIndex = 0;
    public List<string> allModelCoordinates;
    [HideInInspector]
    public Dictionary<string, string> modelIdDatas;
    public List<string> allUrl;
    private ClassVrManager VrManager;
    private int modelDetailsCount;
    private string language;
    List<string> typeOfGames = new List<string>();
    private JSONArray allModelDetails;
    private List<int> modelCoordinateCount = new List<int>();
    private int annotationIncrimenter = 0;
    public GameObject DeviceID;
    public bool isLoadedResponse;
    private VideoManager videoManager;
    public VideoPlayer youtubevideoPlayer;
    public bool is360Image;
   
    public string youtubeURL;
    private AddingImage addingImage;
    private List<float> modelDisplayTime = new List<float>();
    private AssetDownloader assetDownloader;
    private bool isThreeSixty;
    private ThreesixtyImageManager threesixtyImageManager;
    private WebManager webManager;
    private ChangeClassRoom room;
    private string youtubeUrlTest;
    //private YoutubeTestManager youtubeTestManager;

    public string YouTubeUrlTest
    {
        get { return youtubeUrlTest; }
        set { youtubeUrlTest = value; }
    }
    private void Start()
    {
        room = GameManager.Instance.changeClassRoom;
        webManager = GameManager.Instance.webManager;
        threesixtyImageManager = GameManager.Instance.threesixtyImageManager;
        assetDownloader = GameManager.Instance.assetDownloader;
        VrManager = GameManager.Instance.vrManager;
        addingImage = GameManager.Instance.addingImage;
        serviceScript = GameManager.Instance.serviceScript;
        videoManager = GameManager.Instance.videoManager;

        CallingClassVrExperiance();
    }

    public void CallingClassVrExperiance()
    {
        StartCoroutine(WebGetRequest(Klass.Net.KlassEndpoints.V1("experience")));

    }
    public bool IsmodelDownload()
    {
        return isModelDownload;
    }
    public IEnumerator WebGetRequest(string api)
    {

        var apireq = api;
        string jsonData;
        var req = new UnityWebRequest(apireq, "GET");
        //  byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(SystemInfo.deviceUniqueIdentifier);
        // req.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        req.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        //uncomment device id
         req.SetRequestHeader("device-id", SystemInfo.deviceUniqueIdentifier);
       // req.SetRequestHeader("device-id","aj4y");
       
        req.certificateHandler = new BypassCertificateHandler();

        req.downloadHandler = new DownloadHandlerBuffer();
        yield return req.Send();
        if (req.isNetworkError) // error in request
        {
            DebugUtil.Log("Error While Sending: " + req.error);
            // Report download failure to teacher dashboard via MQTT.
            var synHandler = FindObjectOfType<DeviceSynHandler>();
            if (synHandler != null)
                synHandler.PublishDownloadError("Network error: " + req.error);
        }
        else
        {


            jsonData = req.downloadHandler.text;
            Debug.Log("JsonData:"+jsonData);
            bool parseAsArray = false;
            try
            {
                // Attempt to parse the response JSON as an object
                var responseData = JsonUtility.FromJson<ApiResponse>(req.downloadHandler.text);
                DebugUtil.Log("Response Data: " + responseData);
                DeviceID.SetActive(true);
                DeviceIDManager deviceIDManager = DeviceID.GetComponent<DeviceIDManager>();
                deviceIDManager.OneTimeDeviceConfiguration();
            }
            catch (Exception)
            {
                parseAsArray = true;
            }

            if (parseAsArray)
            {
                if (DeviceSynHandler.isOpenWelcomeScreen == false && isLoadedResponse != true)
                {
                    // Defensive parse: bail early if the server returned anything
                    // that isn't a JSON array with at least one experience entry.
                    // Previously a malformed/empty body crashed the VR app at
                    // `spaceData[0]["session"]` before the loading panel showed.
                    JSONNode spaceData = null;
                    bool isParsingDone = false;
                    Task.Run(() => {
                        try { spaceData = JSONNode.Parse(jsonData); } catch { spaceData = null; }
                        isParsingDone = true;
                    });
                    yield return new WaitUntil(() => isParsingDone);
                    if (spaceData == null || spaceData.Count == 0 || spaceData[0] == null)
                    {
                        DebugUtil.Log("[API] Experience response was empty or unparseable — aborting load.");
                        var synHandler = FindObjectOfType<DeviceSynHandler>();
                        if (synHandler != null)
                            synHandler.PublishDownloadError("Empty experience response from server");
                        yield break;
                    }

                    isLoadedResponse = true;
                    DeviceIDManager deviceIDManager = DeviceID.GetComponent<DeviceIDManager>();
                    deviceIDManager.ToggleFunction(true);
                    deviceIDManager.gameObject.SetActive(false);
                    // LoadingScene.SetActive(true);
                    JSONNode session = spaceData[0]["session"];
                    string sessionId = session != null ? session["_id"].Value : "";
                    string sessionTimeAndDate = session != null ? session["sessionTimeAndDate"].Value : "";

                    JSONArray assessments = spaceData[0]["assessments"].AsArray;
                    if (assessments != null && assessments.Count > 0)
                    {
                        foreach (JSONNode assessment in assessments)
                        {
                            string typeOfGame = assessment["typeOfGame"];
                            typeOfGames.Add(typeOfGame);
                        }

                        if (assessments != null && assessments.Count > 0)
                        {
                            assessmentsString = assessments.ToString();
                        }
                    }


                    JSONArray contents = spaceData[0]["content"].AsArray;
                    if (contents == null)
                    {
                        DebugUtil.Log("[API] No 'content' array in experience response — aborting load.");
                        var synHandler2 = FindObjectOfType<DeviceSynHandler>();
                        if (synHandler2 != null)
                            synHandler2.PublishDownloadError("Experience has no content");
                        yield break;
                    }


                    foreach (JSONNode content in contents)
                    {
                        string script = content["script"];
                        language = content["language"];
                        //  YouTube details


                        string youTubeUrl = content["youTubeUrl"];
                        youTubeUrl = AddingURL(youTubeUrl);
                        YouTubeUrlTest = youTubeUrl;

                        string youTubeDownloadableUrl = content["youTubeDownloadedUrl"];

                        //uncomment this for checking youtube status
                        //    youtubeTestManager.CheckVideostatus();


                        bool youTubeVideoAudio = content["youTubeVideoAudio"].AsBool;
                        string youTubeVideoScript = content["youTubeVideoScript"];
                        string youTubeStartTimer = content["youTubeStartTimer"];
                        string youTubeEndTimer = content["youTubeEndTimer"];
                        string classEnvironment = content["classEnvironment"].Value;
                        string youTubeVideoStatus = content["youTubeDownloadStatus"];
                        //uncomment this for assign different room
                        room.AssignClassRoom(classEnvironment);

                        JSONArray modelDetails = content["modelDetails"].AsArray;
                        if (modelDetails != null)
                        {

                        }
                        modelDetailsCount = modelDetails != null ? modelDetails.Count : 0;
                        VrManager.AddDownloadManager(modelDetailsCount);
                        List<string> allAnnotations = new List<string>();
                        JSONArray selectedSimulationDetails = null;

                         JSONArray simulationDetails = content["simulationDetails"].AsArray;
                         JSONArray scholarLabSimulationDetails = content["scholarlabSimulationDetails"].AsArray;
                         JSONArray objectXRDetails = content["objectxrSimulationDetails"].AsArray;
 
                         // Priority:
                         // 1. simulationDetails
                         // 2. scholarlabSimulationDetails
                         // 3. objectXRDetails
 
                         if (simulationDetails != null && simulationDetails.Count > 0)
                         {
                             webManager.isobjectXR = false;
                             selectedSimulationDetails = simulationDetails;
                         }
                         else if (scholarLabSimulationDetails != null && scholarLabSimulationDetails.Count > 0)
                         {
                            webManager.isobjectXR = false;
                            selectedSimulationDetails = scholarLabSimulationDetails;
                         }
                         else if (objectXRDetails != null && objectXRDetails.Count > 0)
                         {
                            webManager.isobjectXR = true;
                            selectedSimulationDetails = objectXRDetails;
                         }
 
                         if (selectedSimulationDetails != null)
                         {
                             foreach (JSONNode simulationDetail in selectedSimulationDetails)
                             {
                                 string simulationScript = simulationDetail["script"];
                                 string displayTime = simulationDetail["displayTime"];
                                 string simulationURL = string.Empty;
 
                                 // Normal Simulation
                                 if (simulationDetail["simulationId"] != null &&
                                     !string.IsNullOrEmpty(simulationDetail["simulationId"]["simulationURL"]))
                                 {
                                     simulationURL = simulationDetail["simulationId"]["simulationURL"];
                                 }
                                 // ScholarLab Simulation
                                 else if (!string.IsNullOrEmpty(simulationDetail["simulationURL"]))
                                 {
                                     simulationURL = simulationDetail["simulationURL"];
                                 }
                                 // Object XR Simulation (direct url)
                                 else if (!string.IsNullOrEmpty(simulationDetail["simulationUrl"]))
                                 {
                                     simulationURL = simulationDetail["simulationUrl"];
                                 }
 
                                 if (!string.IsNullOrEmpty(simulationURL))
                                 {
                                     webManager.AssignSimulationAsync(displayTime, simulationURL, simulationScript);
 
                                     DebugUtil.Log("Simulation URL: " + simulationURL);
                                     DebugUtil.Log("Display Time: " + displayTime);
                                 }
                             }
                         }

                        JSONArray imageDetails = content["imageDetails"].AsArray;
                        

                        // Check if imageDetails is not null and has elements
                        if (imageDetails != null && imageDetails.Count > 0)
                        {
                            isThreeSixty=true;
                            GameManager.Instance.isImageSkip = false;
                            // Access the imageURL field from the first element of imageDetails
                            string imageTime  = imageDetails[0]["displayTime"];
                            string imageUrl = imageDetails[0]["ImageId"]["imageURL"];
                            string imageScript = imageDetails[0]["script"];
                            DebugUtil.Log("Image URL: " + imageUrl);
                            if(!string.IsNullOrEmpty(imageUrl))
                            {
                                is360Image = true;
                                addingImage.LoadAndAssignImage(imageUrl, imageScript,imageTime);
                            }
                        }
                        // Iterate over the modelDetails array
                        if (modelDetails != null) foreach (JSONNode modelDetail in modelDetails)
                        {
                            string modelTimer = modelDetail["displayTime"];
                            if (modelTimer != null)
                            {
                                string[] timeParts = modelTimer.Split(':');

                                int minutes = int.Parse(timeParts[0]);
                                int seconds = int.Parse(timeParts[1]);


                                int totalSeconds = minutes * 60 + seconds;
                                float totalSecondsFloat = (float)totalSeconds;
                                modelDisplayTime.Add(totalSecondsFloat);
                                PlayerPrefs.SetFloat("totslTime", totalSecondsFloat);

                            }
                            //   float floatTimer = float.Parse(modelTimer);
                            // PlayerPrefs.SetFloat(floatTimer);
                            if (modelDetail["modelId"] != null)
                            {
                                string url = modelDetail["modelId"]["modelUrl"];
                                allUrl.Add(url);
                            }
                            // Check if modelCoordinates exists and is not null
                            if (modelDetail["modelCoordinates"] != null)
                            {
                                // Now check if annotations exist within modelCoordinates
                                JSONNode annotationsNode = modelDetail["modelCoordinates"];
                                if (annotationsNode != null && annotationsNode.AsArray.Count > 0) // Making sure annotations is not null and has elements
                                {
                                    string annotationsJson = annotationsNode.ToString();
                                    modelCoordinateCount.Add(annotationIncrimenter);
                                    allAnnotations.Add(annotationsJson);
                                }
                            }
                            annotationIncrimenter++;
                        }
                        assetDownloader.FetchingTimer(modelDisplayTime);


                        if (modelDetails != null && modelDetails.Count > 0)
                        {
                           
                            isModelDownload = true;
                            threesixtyImageManager.ModelStatus(isModelDownload);
                            allModelDetails = modelDetails;
                            ProcessNextModelDetail();

                        }
                        else
                        {
                            isModelDownload = false;

                        }

                        JSONNode videoDetail = content["videoDetails"][0];
                        if (videoDetail != null && videoDetail.Count > 0)
                        {
                            isPlayingVideo = true;
                            UpdateVideoPreferences(videoDetail);
                        }
                        else if(!string.IsNullOrEmpty(youTubeUrl)&& !string.IsNullOrEmpty(youTubeDownloadableUrl))
                        {
                            isPlayingVideo = true;
                           
                            
                          //  youTubeUrl = youtubevideoPlayer.url;
                            UpdateYoutubePreferance(youTubeVideoAudio, youTubeVideoScript, youTubeStartTimer, youTubeEndTimer,youTubeUrl, youTubeDownloadableUrl, youTubeVideoStatus);
                         
                        }
                        else
                        {
                            isPlayingVideo = false;

                        }

                        teacherCharacterGender = content["teacherCharacterGender"];
                        PlayerPrefs.SetString("Speak", script);
                        PlayerPrefs.SetString("ImageScript", content["imageScript"]); // Check if this is correct
                                                                                      //  StartCoroutine(WaitUntil(allAnnotations));
                        int value = PlayerPrefs.GetInt("InSimulation", 0);
                        bool isResuming = DeepLinkManager.isResumingFromDeepLink || (PlayerPrefs.GetInt("InSimulation", 0) == 1);
                        if (isResuming)
                        {
                            PlayerPrefs.SetInt("InSimulation", 0);
                            PlayerPrefs.Save();
                            DeepLinkManager.ResumeExperience();
                        }
                        else
                        {
                            if (!isPlayingVideo && !isModelDownload)
                            {
                                serviceScript.AssignTaskBasedOnCondition(GameStatus.SkipVideoAndModel);
                            }
                            else
                            {
                                if (isModelDownload)
                                {
                                    GameManager.Instance.isModelSkip = true;
                                    serviceScript.AssignAnnotation(allAnnotations, modelCoordinateCount);
                                }
                                else
                                {
                                    GameManager.Instance.isModelSkip = false;
                                    serviceScript.AssignTaskBasedOnCondition(GameStatus.DisbleModelScript);
                                }

                                if (isPlayingVideo)
                                {
                                    serviceScript.AssignTaskBasedOnCondition(GameStatus.AssignVideoStatusAsNotSkip);
                                }
                                else
                                {
                                    serviceScript.AssignTaskBasedOnCondition(GameStatus.AssignVideoStatusAsSkip);
                                }
                            }
                        }
                        
                        // Yield to the main thread to render the VR frame before processing the next content
                        yield return null;
                    }

                    serviceScript.QueryApiAndPassResult(teacherCharacterGender);
                    VrManager.PreferredLanguage(language);
                    //boardParent.SetActive(true);
                }
                else
                {
                    DebugUtil.Log("notsynched");
                }
                // If parsing as an object fails, it's likely a plain string response

                //  VrManager.AssignValues(typeOfGames);


            }
        }


    }


   
    private void UpdateModelPreferences(List<Dictionary<string, string>> modelIds)
    {
        string modelScript = modelDetail["script"];
        JSONNode modelId = modelDetail["modelId"];
        string modelName = modelId["modelName"];
        string modelUrl = modelId["modelUrl"].Value.Trim();
        string thumbnailUrl = modelId["thumbnailUrl"];
        PlayerPrefs.SetString(StaticStrings.modelName, modelName);
        PlayerPrefs.SetString(StaticStrings.modelUrl, modelUrl);
        PlayerPrefs.SetString(StaticStrings.thumbnailUrl, thumbnailUrl);
        PlayerPrefs.SetString("ModelScript", modelScript);
    }


    public string AddingURL(string url)
    {
        youtubeURL = url;
        return url;
    }

    //Assign all modewl details here
    public void ProcessNextModelDetail()
    {
        if (allModelDetails != null && currentIndex < allModelDetails.Count)
        {
            modelDetail = allModelDetails[currentIndex];
            Dictionary<string, string> modelIdData = new Dictionary<string, string>();

            // Extract modelId node
            JSONNode modelIdNode = modelDetail["modelId"];
            modelIdData["id"] = modelIdNode["_id"];
            modelIdData["modelName"] = modelIdNode["modelName"];
            modelIdData["description"] = modelIdNode["description"];
            modelIdData["modelUrl"] = modelIdNode["modelUrl"];
            modelIdData["thumbnailUrl"] = modelIdNode["thumbnailUrl"];
            modelIdData["script"] = modelDetail["script"];

            UpdateModelPreferences(modelIdData);
            modelIdDatas = modelIdData;
            currentIndex++; // Increment the index for the next call
        }
        else
        {
            DebugUtil.Log("No more modelDetails to process or modelDetails is null.");
        }
    }
    public void MultipleModelCall()
    {
        // UpdateModelPreferences(modelIdDatas);
        ProcessNextModelDetail();
    }
    private void UpdateModelPreferences(Dictionary<string, string> modelId)
    {
        string modelName = modelId["modelName"];
        string modelUrl = !string.IsNullOrEmpty(modelId["modelUrl"]) ? modelId["modelUrl"].Trim() : "";
        string thumbnailUrl = modelId["thumbnailUrl"];
        string modelScript = modelId["script"];

        PlayerPrefs.SetString(StaticStrings.modelName, modelName ?? "");
        PlayerPrefs.SetString(StaticStrings.modelUrl, modelUrl);
        PlayerPrefs.SetString(StaticStrings.thumbnailUrl, thumbnailUrl ?? "");
        PlayerPrefs.SetString("ModelScript", modelScript ?? "");
        PlayerPrefs.Save(); // Save the changes to PlayerPrefs
    }
    private void UpdateVideoPreferences(JSONNode videoDetail)
    {
        string videoScript = videoDetail["script"];
        JSONNode videoId = videoDetail["VideoId"];
        string videoURL = (videoId != null && videoId["videoURL"] != null) ? videoId["videoURL"].Value.Trim() : "";
        string videoTitle = videoId != null ? (string)videoId["title"] : "";
        string videoDescription = videoId != null ? (string)videoId["description"] : "";
        string videoThumbnail = videoId != null ? (string)videoId["thumbnail"] : "";
        string videoType = videoId != null ? (string)videoId["typeOfVideo"] : "";
        string videoSound = videoDetail["videoSound"];
        // VrManager.UpdateVideoType(videoType);
        PlayerPrefs.SetString(StaticStrings.videoUrl, videoURL ?? "");
        PlayerPrefs.SetString(StaticStrings.videoScript, videoScript ?? "");
        PlayerPrefs.SetString(StaticStrings.videoSound, videoSound ?? "");
    }
    public void UpdateYoutubePreferance(bool youTubeVideoAudio, string youTubeVideoScript, string youTubeStartTimer, string youTubeEndTimer,string youTubeUrl,string youTubeDownloadableUrl,string status)
    {
        PlayerPrefs.SetString(StaticStrings.videoUrl, youTubeDownloadableUrl);
        PlayerPrefs.SetString(StaticStrings.videoScript, youTubeVideoScript);
        videoManager.AssignYoutubeProperty(youTubeStartTimer, youTubeEndTimer, youTubeVideoAudio, youTubeUrl, youTubeDownloadableUrl, youTubeVideoScript,status);

    }
  
}
public class BypassCertificateHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // Always accept
        return true;
    }
}