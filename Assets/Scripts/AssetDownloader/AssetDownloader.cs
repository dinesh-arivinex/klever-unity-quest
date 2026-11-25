
using System.Collections;
using UnityEngine;

using UnityEngine.Events;

using TriLibCore.Samples;
using static ElevenLabsTTS_Manager;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using SimpleJSON;
using static UnityEngine.GraphicsBuffer;
using System.Collections.Generic;
using static ChangeClassRoom;

public class AssetDownloader : MonoBehaviour
{
  
    public Transform instantiationPoint;
    private GoogleTextToSpeech googleTextToSpeech;
    private AnnotationManager annotationManager;
    public  RuntimeImportBehaviourHelper[] runtimeImportBehaviourHelper;

    //timer

    public float timeRemaining = 10;
    public bool timerIsRunning = false;
    public TextMeshProUGUI timerText;
    public  ServiceScript service;
    private bool isMCQ=true;

    private int count = 3; // How many times to call the function
    private float interval; // Time between function calls
    private float nextThreshold; // Next time to call the function
    private int timesCalled = 0; //
    private int modelcounter;
    public  API api;
    private int modelCount;
    private JSONArray modelDetails;
    private GameObject model;
    private List<float> modelTimer=new List<float>();
    private List<float> thresholds ;
    private float currentTimerIndex;
    private WebManager webManager;
    public TextMeshProUGUI text;

    private void Start()
    {
        webManager = GameManager.Instance.webManager;
        annotationManager = GameManager.Instance.annotationManager;
        googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
    }
    public void TimerStart()
    {
        timeRemaining = 0;
        foreach (float time in modelTimer)
        {
            timeRemaining += time;
        }

        if (timeRemaining == 0)
        {
            timeRemaining = 120f; // Fallback default time
        }

        count = modelTimer.Count; // Set count based on the number of intervals in the modelTimer list
        timesCalled = 0;
        timerIsRunning = true;
        if(modelTimer.Count>=2)
        {
            currentTimerIndex = timeRemaining - modelTimer[0];
        }
        
    }

    public void DownloadingAssetBackgrount()
    {
        if(api.IsmodelDownload())
        {
            //ChangeModelPosition();
            ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[ChangeClassRoom.Instance.classRoom]);
            DynamicGI.UpdateEnvironment();
            annotationManager.initialization();
            StartCoroutine(AssinOperation());
            timerIsRunning = true;
        }
       
    }
    public void FetchingTimer(List<float> timer)
    {
        modelTimer=timer;
    }
    public void StartNextOperation()
    {
        
    }
    private IEnumerator AssinOperation()
    {
        googleTextToSpeech.StartTalking(PlayerPrefs.GetString("ModelScript"));
        yield return new WaitUntil(() => googleTextToSpeech.IsAudioReady);
      
       
       
       
    }
    public void AssignMCQStatus(bool value)
    {
        isMCQ = value;
    }
    private void Perform()
    {
        Klass.Net.LiveViewPublisher.Instance?.CompleteContent("model3d");
        webManager.StartSimulation();
      
    }
    public void AssignModelDetails(JSONArray modelData)
    {
        modelDetails = modelData;
    }
    void FixedUpdate()
    {
        if (timerIsRunning)
        {
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;

                // Check if we've crossed the next threshold to call the function
                if (timesCalled < modelTimer.Count && timeRemaining <= currentTimerIndex)
                {
                    int counter;
                    CallFunction(); // Your function to call
                    timesCalled++;
                    counter = timesCalled + 1;
                    if (modelTimer.Count >= counter)
                    {
                        currentTimerIndex = timeRemaining - modelTimer[timesCalled];
                        
                    }
                  
                }

                DisplayTime(timeRemaining); // Assume this method correctly displays the remaining time
            }
            else
            {
                timerIsRunning = false;
                timeRemaining = 0;
                CallFunction();
                DebugUtil.Log("Time has run out!");
                Perform(); // Perform whatever needs to be done when time runs out
            }
        }
    }

    void DisplayTime(float timeToDisplay)
    {
        timeToDisplay += 1;

        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);

        if (timerText != null)
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    void CallFunction()
    {
        Klass.Net.LiveViewPublisher.Instance?.CompleteContent("model3d");
        if (text != null)
            text.text = "";
        int availableModelCount = api != null && api.allUrl != null ? api.allUrl.Count : 0;
        if (modelCount >= availableModelCount)
            return;
        
        // We are deleting the previous model, that's why we increment "modelCount" after the "if" loop. If "allUrl" is less than 3, then we need to keep models until the timer ends.


        RuntimeImportBehaviourHelper currentHelper = GetImportHelper(modelCount);
        if (currentHelper != null)
            currentHelper.DisableObject();

        // The variable "modelCount" is used to check the total number of models. Then, the "DisableObject()" method of the runtime import behavior helper corresponding to the model count is invoked.
        modelCount++;
       

        //The "apiUrlCount" variable is used to understand the number of available models from the API. Here, we check if the model count is less than the total number of URLs retrieved from the API.

        if (modelCount < availableModelCount)
        {
            api.MultipleModelCall();
            RuntimeImportBehaviourHelper nextHelper = GetImportHelper(modelCount);
            if (nextHelper != null)
                nextHelper.customCallBack(PlayerPrefs.GetString("ModelScript"));
          
            if (annotationManager != null)
                annotationManager.MultipleModelCall(modelCount);
          
        }
    
       


    }

    private RuntimeImportBehaviourHelper GetImportHelper(int index)
    {
        if (runtimeImportBehaviourHelper != null && index >= 0 &&
            index < runtimeImportBehaviourHelper.Length && runtimeImportBehaviourHelper[index] != null)
            return runtimeImportBehaviourHelper[index];

        Debug.LogWarning("[AssetDownloader] No model helper configured at index " + index + ".");
        return null;
    }

    public void PublishVisibleModel()
    {
        string modelUrl = PlayerPrefs.GetString(StaticStrings.modelUrl);
        var details = new SimpleJSON.JSONObject();
        details["name"] = PlayerPrefs.GetString(StaticStrings.modelName);
        Klass.Net.LiveViewPublisher.Instance?.ShowContent("model3d", modelUrl, modelUrl, details);
    }
    public void ChangeModelPosition()
    {
        GameObject collectionPopint = GameObject.Find("CollectionPointCloneScene");
        if (collectionPopint == null || instantiationPoint == null) return;

        // Calculate bottom of the entire model (collection point and its hierarchy)
        Vector3 collectionBottom = GetBottomPosition(collectionPopint);

        // Target bottom from the instantiation point
        Vector3 targetBottom = GetBottomPosition(instantiationPoint.transform.gameObject);

        // Offset needed to align bottoms
        Vector3 offset = collectionBottom - targetBottom;

        // Move the entire collectionPopint without changing child structure
        collectionPopint.transform.position -= offset;

        // No deep-child indexing: glTF assets have different hierarchy depths.
    }

    IEnumerator ChildZero(Transform child )
    {
        yield return new WaitForSeconds(3f);
      //  child.transform.localPosition = Vector3.zero;
    }
    Vector3 GetBottomPosition(GameObject obj)
    {
        // Try getting all renderers first
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer rend in renderers)
            {
                bounds.Encapsulate(rend.bounds);
            }

            return bounds.center - Vector3.up * bounds.extents.y;
        }

        // If no renderers, try colliders
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            foreach (Collider col in colliders)
            {
                bounds.Encapsulate(col.bounds);
            }

            return bounds.center - Vector3.up * bounds.extents.y;
        }

        DebugUtil.LogWarning("No Renderer or Collider found in hierarchy: " + obj.name);
        return obj.transform.position;
    }
}
