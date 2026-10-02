using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using Vuplex.WebView;
using static ChangeClassRoom;
using static UnityEngine.Rendering.DebugUI;

public class WebManager : MonoBehaviour
{
    private const string ObjectXLauncherUrl = "https://klever-edu.com/objectx-launcher.html";
    private const string AnubhavLauncherUrl = "/admin.html";
    private GamificationManager gamificationManager;
    public GameObject webView;
    public GameObject[] disableitems;
    public CanvasWebViewPrefab _webViewPrefab;
    private string url;
    private float simulationTimer;
    private ServiceScript service;
    public CanvasWebViewLoader canvasWebViewLoader;
    private string simulationScript;
    private GoogleTextToSpeech googleTextToSpeech;
    public TextMeshProUGUI timer;
    private Coroutine timerCoroutine;
    public GameObject buttonNext;
    private bool oneMinutePassed;
    public UnityEvent startingevent;
    public UnityEvent endevent;
    public GameObject simulation;
    public GameObject temp;
    private bool firstTime;
    public bool isobjectXR;
    // Start is called before the first frame update
    void Start()
    {
        googleTextToSpeech = GameManager.Instance.googleTextToSpeech;
        service = GameManager.Instance.serviceScript;
        gamificationManager = GameManager.Instance.gamificationManager;


    }
    public float ConvertTimeToSeconds(string displayTime)
    {
        if (string.IsNullOrEmpty(displayTime))
        {
            Debug.LogError("Input time string is null or empty.");
            return 0f;
        }

        // Split the time string into minutes and seconds
        string[] timeComponents = displayTime.Split(':');

        // Check if the time string has the correct format
        if (timeComponents.Length != 2)
        {
            Debug.LogError("Invalid time format. Expected format is MM:ss.");
            return 0f;
        }

        // Parse minutes and seconds from the split string
        if (!int.TryParse(timeComponents[0], out int minutes) || !int.TryParse(timeComponents[1], out int seconds))
        {
            Debug.LogError("Failed to parse minutes or seconds.");
            return 0f;
        }

        // Calculate total seconds
        float totalSeconds = minutes * 60f + seconds;

        return totalSeconds;
    }
    public void StopWebViewAudio()
    {
        if (_webViewPrefab != null && _webViewPrefab.WebView != null && _webViewPrefab.WebView.IsInitialized)
        {
            // Execute JavaScript to stop audio playback within the WebView
            string jsCommand = "var audioElements = document.getElementsByTagName('audio');" +
                               "for(var i = 0; i < audioElements.length; i++) {" +
                               "  audioElements[i].pause();" +
                               "}";

            _webViewPrefab.WebView.ExecuteJavaScript(jsCommand);
        }
    }

    public void AssignSimulationAsync(string time, string newUrl, string script)
    {
        simulationScript = script;
        url = newUrl;
        simulationTimer = ConvertTimeToSeconds(time);

        // ObjectX launches an immersive WebXR session, which Vuplex 3D WebView
        // doesn't support. Keep the WebView path only for non-WebXR simulations.
        if (_webViewPrefab != null && !isobjectXR)
        {
            _webViewPrefab.InitialUrl = url;
            StartLoading();
        }

        StartSimulation();
    }
    public void StartSimulation()
    {
        if (!string.IsNullOrEmpty(url))
        {
            var liveDetails = new SimpleJSON.JSONObject();
            liveDetails["isObjectXR"] = isobjectXR;
            liveDetails["durationSeconds"] = simulationTimer;
            Klass.Net.LiveViewPublisher.Instance?.ShowContent("simulation", url, url, liveDetails);


            if (firstTime)
            {
                ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[ChangeClassRoom.Instance.classRoom]);
                GamificationControllerHandSwitcher.Instance.leftHandRaycast.SetActive(true);
                GamificationControllerHandSwitcher.Instance.rightHandRaycast.SetActive(true);
                GamificationControllerHandSwitcher.Instance.leftControllerRaycast.SetActive(true);
                GamificationControllerHandSwitcher.Instance.rightControllerRaycast.SetActive(true);

                simulation.transform.position = temp.transform.position;
                simulation.transform.rotation = temp.transform.rotation;
                startingevent.Invoke();
                timerCoroutine = StartCoroutine(WebViewDuration(simulationTimer));
                ToggleItem(false);
                googleTextToSpeech.StopAudio();
                if (!string.IsNullOrEmpty(simulationScript))
                {
                    googleTextToSpeech.StartTalking(simulationScript);
                }
                if (isobjectXR)
                {
                    PlayerPrefs.SetInt("InSimulation", 1);
                    PlayerPrefs.Save();
                    webView.SetActive(false);
                    if (url.Contains("vredumelzo"))
                    {
                        Application.OpenURL(BuildAnubhavBrowserUrl(url));
                        StartCoroutine(KleverAppQuit());

                    }
                    else
                    {
                        Application.OpenURL(BuildObjectXBrowserUrl(url));
                        StartCoroutine(KleverAppQuit());

                    }

                }
                else
                {
                    canvasWebViewLoader.SetupWebView(url);
                }
            }
            else
            {
                ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[ChangeClassRoom.Instance.classRoom]);
                if (isobjectXR)
                {
                    // AssignSimulationAsync() preloads normal simulations. ObjectX
                    // must wait until StartSimulation() is called for the real step,
                    // then it is opened by the Quest browser above.
                    webView.SetActive(false);
                }
                else
                {
                    webView.SetActive(true);
                    canvasWebViewLoader.SetupWebView(url);
                }
            }
            firstTime = true;

        }
        else
        {
            CheckTheStatus();
        }

    }

    private string BuildObjectXBrowserUrl(string embedUrl)
    {
        return ObjectXLauncherUrl + "?src=" + Uri.EscapeDataString(embedUrl);
    }

    private string BuildAnubhavBrowserUrl(string embedUrl)
    {
        return embedUrl;
    }

    public void ToggleItem(bool value)
    {

        foreach (GameObject item in disableitems)
        {
            item.SetActive(value);
        }



    }

    private async void StartLoading()
    {
        if (_webViewPrefab != null)
        {
            await _webViewPrefab.WaitUntilInitialized();
            _webViewPrefab.WebView.UrlChanged += (sender, eventArgs) => {
                Debug.Log("[WebManager] URL changed: " + eventArgs.Url);
                if (!string.IsNullOrEmpty(eventArgs.Url) && (eventArgs.Url.Contains("klever://") || eventArgs.Url.Contains("klever-edu") || eventArgs.Url.EndsWith("/home") || eventArgs.Url.Contains("close-simulation")))
                {
                    Debug.Log("[WebManager] Deep link / Home redirect detected: " + eventArgs.Url + ". Closing simulation.");
                    CheckTheStatus();
                }
            };
            _webViewPrefab.WebView.LoadUrl(url);
        }
    }

    IEnumerator WebViewDuration(float duration)
    {

        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            // Calculate minutes and seconds
            int minutes = Mathf.FloorToInt(timeRemaining / 60);
            int seconds = Mathf.FloorToInt(timeRemaining % 60);

            // Update the timer text to display in minutes and seconds format
            timer.text = string.Format("{0}:{1:00}", minutes, seconds);

            // aterter reach one min
            if (timeRemaining <= duration - 60f && !oneMinutePassed)
            {
                oneMinutePassed = true;
                buttonNext.SetActive(true);
            }

            yield return new WaitForSeconds(1f); // Wait for 1 second
            timeRemaining -= 1f; // Decrease remaining time by 1 second
        }

        // After the countdown is complete, check the status
        CheckTheStatus();
    }
    public void CheckTheStatus()

    {
        Klass.Net.LiveViewPublisher.Instance?.HideContent("simulation-completed");
        //PlayerPrefs.SetInt("InSimulation", 0);
        PlayerPrefs.Save();

        endevent.Invoke();
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        StopWebViewAudio();
        // webView.SetActive(false );
        Destroy(webView);
        service.AssignTaskBasedOnCondition(GameStatus.Mcq);
        gamificationManager.AssignGames();
    }
    // Update is called once per frame

    public IEnumerator KleverAppQuit()
    {
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }
}
