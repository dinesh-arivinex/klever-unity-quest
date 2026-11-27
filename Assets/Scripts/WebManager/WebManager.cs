using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Vuplex.WebView;
using static ChangeClassRoom;

public class WebManager : MonoBehaviour
{
    private const float LoadTimeout = 90f;
    private const string PageReadyCheck =
        "(function(){var ready=document.documentElement.getAttribute('data-klever-ready');" +
        "if(document.readyState!=='complete')return false;" +
        "if(ready!==null)return ready==='true';" +
        "if(location.href==='about:blank')return false;" +
        "if(window.AdobeAn&&window.createjs&&document.querySelector('canvas'))" +
        "return !!(window.stage&&window.stage.getNumChildren&&window.stage.getNumChildren()>0);" +
        "return true;})()";
    public GameObject webView;
    public GameObject[] disableitems;
    public CanvasWebViewPrefab _webViewPrefab;
    public TextMeshProUGUI timer;
    public GameObject buttonNext;
    public UnityEvent startingevent;
    public UnityEvent endevent;
    public GameObject simulation;
    public GameObject temp;
    public bool isobjectXR;

    private string url;
    private string simulationScript;
    private float simulationTimer;
    private Coroutine simulationRoutine;
    private IWebView embeddedPage;
    private bool stageEntered;
    private bool stageEnded;
    private bool contentShown;
    private bool applicationPaused;

    public float ConvertTimeToSeconds(string displayTime)
    {
        var parts = displayTime?.Split(':');
        if (parts == null || parts.Length != 2 || !int.TryParse(parts[0], out int minutes) ||
            !int.TryParse(parts[1], out int seconds) || minutes < 0 || seconds < 0 || seconds >= 60)
            return 0f;
        return minutes * 60f + seconds;
    }

    public void AssignSimulationAsync(string time, string newUrl, string script)
    {
        simulationScript = script;
        url = newUrl?.Trim();
        simulationTimer = ConvertTimeToSeconds(time);
    }

    public void StartSimulation()
    {
        if (stageEntered || stageEnded) return;
        stageEntered = true;
        if (string.IsNullOrEmpty(url))
        {
            CheckTheStatus();
            return;
        }

        var room = ChangeClassRoom.Instance;
        if (room != null) ChangeSkyBox.SetSkybox(ChangeSkyBox.skyBoxMaterialNew[room.classRoom]);
        var hands = GamificationControllerHandSwitcher.Instance;
        if (hands != null)
        {
            hands.leftHandRaycast?.SetActive(true);
            hands.rightHandRaycast?.SetActive(true);
            hands.leftControllerRaycast?.SetActive(true);
            hands.rightControllerRaycast?.SetActive(true);
        }
        if (simulation != null && temp != null)
            simulation.transform.SetPositionAndRotation(temp.transform.position, temp.transform.rotation);

        startingevent.Invoke();
        ToggleItem(false);
        GameManager.Instance.googleTextToSpeech?.StopAudio();
        buttonNext.SetActive(false);
        if (isobjectXR)
        {
            BeginContent();
            OpenExternalSimulation();
            return;
        }

        timer.text = "Loading simulation…";
        PrepareEmbeddedView();
        simulationRoutine = StartCoroutine(RunEmbeddedSimulation());
    }

    public void PrepareEmbeddedView()
    {
        // Quest uses the scene-authored floating canvas and Oculus controls.
        // Board fitting and replacement pointer controls belong to the DPVR renderer.
        if (simulation != null && temp != null)
            simulation.transform.SetPositionAndRotation(temp.transform.position, temp.transform.rotation);
        webView.SetActive(true);
        _webViewPrefab.gameObject.SetActive(true);
        _webViewPrefab.Visible = true;
    }

    private IEnumerator RunEmbeddedSimulation()
    {
        // Initialization and navigation have separate limits. Neither consumes
        // the authored activity duration.
        float deadline = Time.realtimeSinceStartup + LoadTimeout;
        var init = _webViewPrefab.WaitUntilInitialized();
        while (!init.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (init.Status != TaskStatus.RanToCompletion || _webViewPrefab.WebView == null)
        {
            yield return FinishUnavailableSimulation();
            yield break;
        }

        embeddedPage = _webViewPrefab.WebView;
        embeddedPage.UrlChanged += OnSimulationUrlChanged;
        bool ready = false;
        yield return WaitForPlayablePage(value => ready = value);
        if (!ready)
        {
            yield return FinishUnavailableSimulation();
            yield break;
        }

        // Unity can initialize the web view while the application is paused.
        // Start tracking only once the learner can see and use the board.
        yield return new WaitUntil(() => !applicationPaused && !LessonPause.IsPaused);
        BeginContent();
        Debug.Log("[SIMULATION] Quest WebView ready; authored duration started.");
        if (simulationTimer <= 0f)
        {
            timer.text = string.Empty;
            buttonNext.SetActive(true);
            yield break;
        }
        yield return WebViewDuration(simulationTimer);
    }

    private IEnumerator WaitForPlayablePage(Action<bool> complete)
    {
        var deadline = Time.realtimeSinceStartup + LoadTimeout;
        Task pageLoad;
        try
        {
            pageLoad = embeddedPage.WaitForNextPageLoadToFinish();
            embeddedPage.LoadUrl(url);
        }
        catch (Exception error)
        {
            Debug.LogWarning("[Simulation] Navigation failed: " + error.GetType().Name);
            complete(false);
            yield break;
        }

        while (!pageLoad.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (pageLoad.Status == TaskStatus.RanToCompletion)
        {
            while (Time.realtimeSinceStartup < deadline)
            {
                var check = embeddedPage.ExecuteJavaScript(PageReadyCheck);
                while (!check.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                if (check.Status == TaskStatus.RanToCompletion &&
                    string.Equals(check.Result?.Trim('"'), "true", StringComparison.OrdinalIgnoreCase))
                {
                    complete(true);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }
        else if (pageLoad.IsFaulted)
        {
            Debug.LogWarning("[Simulation] Page load failed: " + pageLoad.Exception.GetBaseException().GetType().Name);
        }
        complete(false);
    }

    private void BeginContent()
    {
        var details = new SimpleJSON.JSONObject();
        details["isObjectXR"] = isobjectXR;
        details["durationSeconds"] = simulationTimer;
        Klass.Net.LiveViewPublisher.Instance?.ShowContent("simulation", url, url, details);
        contentShown = true;
        if (!string.IsNullOrEmpty(simulationScript))
            GameManager.Instance.googleTextToSpeech?.StartTalking(simulationScript);
    }

    private IEnumerator FinishUnavailableSimulation()
    {
        timer.text = "Simulation unavailable. Continuing shortly…";
        GameManager.Instance.deviceSynHandler?.PublishDownloadError("Simulation did not finish loading.");
        yield return new WaitForSeconds(4f);
        FinishStage(false);
    }

    private IEnumerator WebViewDuration(float duration)
    {
        float elapsed = 0f;
        int displayed = -1;
        while (elapsed < duration)
        {
            int seconds = Mathf.CeilToInt(duration - elapsed);
            if (seconds != displayed)
            {
                displayed = seconds;
                timer.text = $"{seconds / 60}:{seconds % 60:00}";
            }
            if (elapsed >= 60f && !buttonNext.activeSelf) buttonNext.SetActive(true);
            yield return null;
            if (!applicationPaused && !LessonPause.IsPaused) elapsed += Time.deltaTime;
        }
        CheckTheStatus();
    }

    private void OpenExternalSimulation()
    {
        PlayerPrefs.SetInt("InSimulation", 1);
        PlayerPrefs.Save();
        webView.SetActive(false);
        timer.text = "Return to Klever when you finish";
        Application.OpenURL(url);
        StartCoroutine(KleverAppQuit());
    }

    private void OnSimulationUrlChanged(object sender, UrlChangedEventArgs args)
    {
        if (DeepLinkManager.IsSimulationReturnUrl(args.Url)) CheckTheStatus();
        else if (Uri.TryCreate(args.Url, UriKind.Absolute, out var destination) &&
                 Uri.TryCreate(url, UriKind.Absolute, out var origin) &&
                 string.Equals(destination.Host, origin.Host, StringComparison.OrdinalIgnoreCase) &&
                 destination.AbsolutePath == "/close-simulation")
            CheckTheStatus();
    }

    public void ToggleItem(bool visible)
    {
        foreach (var item in disableitems)
            if (item != null) item.SetActive(visible);
    }

    public void StopWebViewAudio()
    {
        if (embeddedPage != null && embeddedPage.IsInitialized)
            embeddedPage.ExecuteJavaScript(
                "Array.prototype.forEach.call(document.querySelectorAll('audio,video'),function(media){media.pause();});", null);
    }

    public void CheckTheStatus() => FinishStage(true);

    private void FinishStage(bool completed)
    {
        if (stageEnded) return;
        stageEnded = true;
        if (simulationRoutine != null)
        {
            StopCoroutine(simulationRoutine);
            simulationRoutine = null;
        }
        if (contentShown)
            Klass.Net.LiveViewPublisher.Instance?.HideContent(completed ? "simulation-completed" : "simulation-unavailable");
        StopWebViewAudio();
        Unsubscribe();
        if (webView != null) Destroy(webView);
        endevent.Invoke();
        GameManager.Instance.serviceScript.AssignTaskBasedOnCondition(GameStatus.Mcq);
        GameManager.Instance.gamificationManager.AssignGames();
    }

    private void OnApplicationPause(bool paused) => applicationPaused = paused;

    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        if (embeddedPage == null) return;
        embeddedPage.UrlChanged -= OnSimulationUrlChanged;
        embeddedPage = null;
    }

    public IEnumerator KleverAppQuit()
    {
        yield return new WaitForSeconds(1f);
        Application.Quit();
    }
}
