using System;
using UnityEngine;

public class DeepLinkManager : MonoBehaviour
{
    public static DeepLinkManager Instance { get; private set; }
    public static bool isResumingFromDeepLink = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("DeepLinkManager");
            Instance = go.AddComponent<DeepLinkManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Application.deepLinkActivated += onDeepLinkActivated;
        
        // Handle if started from deep link when app completely restarts
        if (!String.IsNullOrEmpty(Application.absoluteURL))
        {
            onDeepLinkActivated(Application.absoluteURL);
        }
    }

    private void onDeepLinkActivated(string url)
    {
        if (IsSimulationReturnUrl(url) && PlayerPrefs.GetInt("InSimulation", 0) == 1)
        {
            isResumingFromDeepLink = true;

            // If GameManager is already initialized (meaning app was resumed, not restarted)
            // and the API has already loaded the experience
            if (GameManager.Instance != null && GameManager.Instance.api != null && GameManager.Instance.api.isLoadedResponse)
            {
                ResumeExperience();
            }
        }
    }

    // Routing validation only: this URL is not proof that an external lab was
    // completed. The simulation flag must have been set by our launch flow.
    public static bool IsSimulationReturnUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, "klever", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(uri.Host, "webtest", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(uri.UserInfo) && uri.IsDefaultPort &&
            (uri.AbsolutePath == "" || uri.AbsolutePath == "/") &&
            string.IsNullOrEmpty(uri.Fragment);
    }

    private void OnDestroy()
    {
        Application.deepLinkActivated -= onDeepLinkActivated;
        if (Instance == this) Instance = null;
    }

    public static void ResumeExperience()
    {
        if (!isResumingFromDeepLink && PlayerPrefs.GetInt("InSimulation", 0) != 1) return;
        if (GameManager.Instance == null || GameManager.Instance.api == null) return;
        isResumingFromDeepLink = false;
        PlayerPrefs.SetInt("InSimulation", 0);
        PlayerPrefs.Save();

        if (GameManager.Instance != null && GameManager.Instance.api != null)
        {
            string assessments = GameManager.Instance.api.assessmentsString;
            
            if (!string.IsNullOrEmpty(assessments))
            {
                // Has assessments -> trigger MCQ and Gamification
                GameManager.Instance.serviceScript.AssignTaskBasedOnCondition(GameStatus.Mcq);
                GameManager.Instance.gamificationManager.AssignGames();
                var loadingbar = FindAnyObjectByType<LoadingBar>();
                loadingbar.CustomSumulatedLoading();
            }
            else
            {
                // No assessments -> experience marked as completed and ended normally
                GameManager.Instance.gamificationManager.CallingThankyouOutSide();
                GameManager.Instance.gamificationManager.thankyouCanvas.SetActive(true);
                if (GameManager.Instance.AssessmentParent != null)
                {
                    GameManager.Instance.AssessmentParent.SetActive(false);
                }
            }
        }
    }
}
