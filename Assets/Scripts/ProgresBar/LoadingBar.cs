using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LoadingBar : MonoBehaviour
{
    public GameObject loadingScreen;
   // public Slider slider;
    public Image sliderNew;
    public TextMeshProUGUI progressText;
    private RuntimeImportBehaviour importBehaviour;
    public GameObject[] toggleItem;
    private GameObject parentObject;
    public UnityEvent StartGame;
    private int totalCount;
    private bool IsmodelDownload;
    public DeviceSynHandler deviceSynHandler;
    private bool isLoaded;
    public GameObject blackSphere;
    private API api;
    private System.Collections.Generic.List<Behaviour> trackers = new System.Collections.Generic.List<Behaviour>();
    private bool trackingEnabledAt30 = false;
  
    private void Start()
    {
        api=FindObjectOfType<API>();
      //  deviceSynHandler=FindObjectOfType<DeviceSynHandler>();
        parentObject = transform.parent != null ? transform.parent.gameObject : null;
        //importBehaviour =FindAnyObjectByType<RuntimeImportBehaviour>();
        // SimulateLoading();
        ToggleObject(false);

        if (Camera.main != null)
        {
            var componentsToDisable = new string[] { "TrackedPoseDriver", "OVRCameraRig" };
            Transform current = Camera.main.transform;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Behaviour>())
                {
                    if (comp != null)
                    {
                        string compName = comp.GetType().Name;
                        foreach (var target in componentsToDisable)
                        {
                            if (compName.Contains(target))
                            {
                                trackers.Add(comp);
                                comp.enabled = false;
                            }
                        }
                    }
                }
                current = current.parent;
            }
        }
    }
  
    public void CustomSumulatedLoading()
    {
        totalCount = 20;
        SynchDevice();
        StartCoroutine(GenerateRandomProgress());
    }


    public void SimulateLoading(RuntimeImportBehaviour finalScript)
    {
        SynchDevice();
        // Null-guard: when the current experience has no 3D models the caller
        // (DownloadManager/ClassVrManager) may pass null because no
        // RuntimeImportBehaviour was activated. Previously this caused an
        // immediate NullReferenceException at `importBehaviour.totalStep`,
        // crashing the VR app before the loading panel ever appeared.
        importBehaviour = finalScript;
        IsmodelDownload = finalScript != null;
        totalCount = (finalScript != null && finalScript.totalStep > 0)
            ? finalScript.totalStep
            : 20; // fall back to a sane default so the bar still animates
        StartCoroutine(GenerateRandomProgress());
    }

    IEnumerator GenerateRandomProgress()
    {
        if (loadingScreen != null) loadingScreen.SetActive(true);

        int totalProgressSteps = totalCount > 0 ? totalCount : 100;
        // Cap the max loading time to around 3-5 seconds to avoid infinite loading screens
        float waitTime = Mathf.Clamp(5.0f / totalProgressSteps, 0.01f, 0.1f);

        for (int i = 0; i <= totalProgressSteps; i++)
        {
            float progress = Mathf.Clamp01((float)i / totalProgressSteps);
            if (sliderNew != null) sliderNew.fillAmount = progress;
            if (progressText != null) progressText.text = (progress * 100f).ToString("F0") + "%";
            // Broadcast live download progress to the teacher dashboard via MQTT.
            if (deviceSynHandler != null) deviceSynHandler.PublishDownloadProgress(progress);
            
            if (!trackingEnabledAt30 && progress >= 0.30f)
            {
                foreach (var tracker in trackers)
                {
                    if (tracker != null) tracker.enabled = true;
                }
                trackingEnabledAt30 = true;
            }

            yield return new WaitForSecondsRealtime(waitTime);
        }

        // Complete the progress bar at 100%.
        if (sliderNew != null) sliderNew.fillAmount = 1f;
        if (progressText != null) progressText.text = "100%";
        if (deviceSynHandler != null) deviceSynHandler.PublishDownloadProgress(1f);
        isLoaded = true;

        // 1) Mark this device as synced (publishes MQTT state → web console
        //    shows "playing"; also enables Welcome flow checks elsewhere).
        DeviceSync();

        // 2) Hide the loading UI — the user requirement: "Loading bar completes
        //    and needs to be disabled".
        if (loadingScreen != null) loadingScreen.SetActive(false);
        if (blackSphere != null) blackSphere.SetActive(false);
        ToggleObject(true);
        // 3) Fire the StartGame UnityEvent. In the KlassScene prefab this is
        //    wired to RuntimeImportBehaviourHelper.AssetDownloadCallback which
        //    runs FindingModel() + StartSpeaking() (Google TTS) and continues
        //    the experience flow (360 videos, images, etc.).
        //    Guarded by IsmodelDownload so the no-model path (CustomSumulatedLoading)
        //    does not trigger model-binding logic.
        if (IsmodelDownload && StartGame != null)
        {
            try { StartGame.Invoke(); }
            catch (System.Exception e)
            {
                Debug.LogError("[LoadingBar] StartGame.Invoke failed: " + e.Message);
                if (deviceSynHandler != null)
                    deviceSynHandler.PublishDownloadError("Post-load start failed: " + e.Message);
            }
        }
    }
    private void Update()
    {
       
        
    }

    public void DeviceSync()
    {
        if (deviceSynHandler == null)
        {
            deviceSynHandler = FindObjectOfType<DeviceSynHandler>();
            if (deviceSynHandler == null)
            {
                Debug.LogError("[LoadingBar] DeviceSynHandler reference missing — cannot mark device synced.");
                return;
            }
        }
        deviceSynHandler.SynDevice();
    }
    public void SynchDevice()
    {
        if (parentObject == null && transform.parent != null)
            parentObject = transform.parent.gameObject;
        if (parentObject != null) parentObject.SetActive(true);
        gameObject.SetActive(true);
        if (loadingScreen != null) loadingScreen.SetActive(true);
        if (blackSphere != null) blackSphere.SetActive(false);
        if (sliderNew != null) sliderNew.fillAmount = 0f;
        if (progressText != null) progressText.text = "0%";
        if (deviceSynHandler == null)
            deviceSynHandler = FindObjectOfType<DeviceSynHandler>();
        trackingEnabledAt30 = false;
        isLoaded = false;
        if (deviceSynHandler != null) deviceSynHandler.PublishDownloadProgress(0f);
    }
    public void ToggleObject(bool isTrue)
    {
        if (isTrue)
        {
            foreach (var item in toggleItem)
            {
                if (item != null) item.SetActive(true);
            }
            if (parentObject != null) parentObject.SetActive(false);

        }
        else
        { 
            foreach (var item in toggleItem)

            {
                if (item != null) item.SetActive(false);
            }
           
        }
            
    }
}
