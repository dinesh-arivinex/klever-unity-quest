using LibTessDotNet;
using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.Networking;

public class BasketBallQuiz : MonoBehaviour
{
    public TextMeshProUGUI questionText;
    public TextMeshProUGUI MCQquestionText;
    public TextMeshProUGUI questionPanelText;
    public Button[] answerButtons;
    public Button nextButton;
    private bool buttonClicked = false;
    public UnityEvent reloadSceneEvent;
    private API apiManager;
    public UnityEvent StartEvent;
    public GameObject elementWantToDelete;
    public UnityEvent nextAction;
    public bool isArrow;
    private int basketballScore;
    private int arrowScore;
    private List<InteractableUnityEventWrapper> interactableWrappers = new List<InteractableUnityEventWrapper>();
    [System.Serializable]
    private class Wrapper
    {
        public List<QuestionData> questions;
    }

    [System.Serializable]
    public struct QuestionData
    {
        public string question;
        public List<AnswerData> options;
        public string createdBy;
        public string sessionId;
        public string id;
        public string questionImageUrl;
    }

   

   // private List<QuestionData> questions;
    private int currentQuestionIndex = 0;
    //public MeshRenderer[] meshRender;
    public bool isBasketBall;
    public GameObject basketBall;
    public GameObject arrow;
    private GamificationManager gamificationManager;
    private List<AnswerData> answers=new List<AnswerData>();
    private void OnEnable()
    {
        gamificationManager = FindAnyObjectByType<GamificationManager>();
        BallSpawner.isGamefication=true;
        apiManager = FindAnyObjectByType<API>();
         Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"questions\":" + apiManager.assessmentsString + "}");
       // questions = wrapper.questions;

        //DisplayQuestion();
        StartEvent.Invoke();
        nextButton.enabled = false;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = false;
        Invoke("DeleteElement", 4f);
        //foreach (var element in meshRender)
        //{
        //    element.enabled = true;
        //}

    }
    private void Start()
    {
        nextButton.enabled = false;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = false;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            NextButtonClick();
        }
    }

    
    private void DisplayQuestionImage(string url)
    {
        Transform parentTransform = questionText != null ? questionText.transform.parent : transform;
        if (parentTransform == null) return;
        Transform existing = parentTransform.Find("DynamicQuestionImage");
        RawImage rawImg = null;
        if (existing != null)
        {
            rawImg = existing.GetComponent<RawImage>();
        }
        else
        {
            GameObject go = new GameObject("DynamicQuestionImage");
            go.transform.SetParent(parentTransform, false);
            rawImg = go.AddComponent<RawImage>();
            
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1.2f, 0.8f); // set size in world space scale, since it's on a VR Canvas
            rect.anchoredPosition = new Vector3(0f, 150f, 0f); // adjust offset relative to question text parent
        }

        if (rawImg != null)
        {
            if (string.IsNullOrEmpty(url))
            {
                rawImg.gameObject.SetActive(false);
            }
            else
            {
                rawImg.gameObject.SetActive(true);
                StartCoroutine(LoadImageCoroutine(url, texture => {
                    rawImg.texture = texture;
                }));
            }
        }
    }

    private void SetOptionImage(Button button, string url)
    {
        Transform existing = button.transform.Find("DynamicOptionImage");
        RawImage rawImg = null;
        if (existing != null)
        {
            rawImg = existing.GetComponent<RawImage>();
        }
        else
        {
            GameObject imgGo = new GameObject("DynamicOptionImage");
            imgGo.transform.SetParent(button.transform, false);
            imgGo.transform.SetSiblingIndex(0); // Draw behind text
            rawImg = imgGo.AddComponent<RawImage>();
            
            RectTransform rect = imgGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);
        }

        if (rawImg != null)
        {
            if (string.IsNullOrEmpty(url))
            {
                rawImg.gameObject.SetActive(false);
            }
            else
            {
                rawImg.gameObject.SetActive(true);
                StartCoroutine(LoadImageCoroutine(url, texture => {
                    rawImg.texture = texture;
                }));
            }
        }
    }

    private IEnumerator LoadImageCoroutine(string url, System.Action<Texture2D> onSuccess)
    {
        if (string.IsNullOrEmpty(url)) yield break;
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                onSuccess?.Invoke(texture);
            }
            else
            {
                DebugUtil.LogError("Failed to load image from: " + url + " Error: " + webRequest.error);
            }
        }
    }

    public void DisplayQuestion(List<string> heading, List<AnswerData> answerOptions, string actualQuestion, string questionImageUrl)
    {
        answers = answerOptions;
        questionText.text = actualQuestion;
        DisplayQuestionImage(questionImageUrl);

        if (questionText != null && currentQuestionIndex < 1)
        {
            questionPanelText.text = heading[1];
        }

        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < answerOptions.Count)
            {


                if (answerButtons[i] != null)
                {
                    answerButtons[i].gameObject.SetActive(true);

                    TextMeshProUGUI buttonText = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = answerOptions[i].text;
                    }
                    else
                    {
                        DebugUtil.LogError("TextMeshProUGUI component not found on the button.");
                    }

                    SetOptionImage(answerButtons[i], answerOptions[i].imageUrl);

                    int answerIndex = i;
                    AssessmentButtonBinding.Bind(answerButtons[i], answerIndex, OnAnswerClick);



                }
                else
                {
                    DebugUtil.LogError("Button is null at index " + i);
                }
            }
            else if (answerButtons[i] != null)
            {
                answerButtons[i].gameObject.SetActive(false);
            }
        }

        buttonClicked = false;
    }

    private void OnAnswerClick(int selectedAnswerIndex)
    {
        if (!buttonClicked && !LessonPause.IsPaused)
        {
            var response = new SimpleJSON.JSONObject();
            response["selectedIndex"] = selectedAnswerIndex;
            response["selectedText"] = answers[selectedAnswerIndex].text;
            response["selectedOptionId"] = answers[selectedAnswerIndex]._id ?? string.Empty;
            response["isCorrect"] = answers[selectedAnswerIndex].isCorrect;
            Klass.Net.LiveViewPublisher.Instance?.PublishAssessmentInteraction(gamificationManager.gameCount, response, true);

            Button clickedButton = answerButtons[selectedAnswerIndex];
            // Create colors with adjusted alpha values
            Color greenWithAlpha = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
            Color redWithAlpha = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);
            
            // Change button colors for all available options
            for (int i = 0; i < answers.Count; i++)
            {
                if (answerButtons[i] != null && answerButtons[i].gameObject.activeSelf)
                {
                    ChangeButtonColor(answerButtons[i], answers[i].isCorrect ? greenWithAlpha : redWithAlpha);
                }
            }

            if (answers[selectedAnswerIndex].isCorrect)
            {
                if(isArrow)
                {
                    //  arrowScore++;
                    gamificationManager.arrowScore++;
                }
                else
                {
                    gamificationManager.basketBallScore++;
                  //  basketballScore++;
                }
            }
            buttonClicked = true;
            ToggleButtonActivation(false);
        }

    }
 
    public void ToggleButtonActivation(bool activated)
    {
        foreach (var button in answerButtons)
        {
            button.enabled = activated;
        }
    }

    public void NextButtonClick()
    {
        
        nextButton.enabled = false;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = false;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            Color newColor=  new Color(150f / 255f, 150f / 255f, 150f / 255f, 255f / 255f);
            StartCoroutine( RestoreButtonColor(answerButtons[i], newColor));
            DebugUtil.Log(answerButtons[i].gameObject.name);
          
        }
        ToggleButtonActivation(false);
        buttonClicked = false;

        currentQuestionIndex++;
        if (currentQuestionIndex < 1)
        {
          
            // DisplayQuestion();
        }
        else
        {
            if(isArrow)
            {
                //add api here
                DebugUtil.Log(arrowScore);
              
            }
            else
            {
                //add api here
              
                DebugUtil.Log(basketballScore);
            }
            gamificationManager.OnGameCompleted();
            reloadSceneEvent.Invoke();
            currentQuestionIndex = 0;
        }
    }

    private void ChangeButtonColor(Button button, Color color)
    {

        Graphic graphic = button.GetComponent<Graphic>();
        if (graphic != null)
        {
           graphic.color = color;
        }
        else
        {
            DebugUtil.LogError("Graphic component not found on the button.");
        }
    }

    private IEnumerator RestoreButtonColor(Button button, Color color)
    {
        // This is essentially not causing any delay, consider removing or increasing the time if needed

        // Create a new color with baseColor RGB values but with the Alpha set to 95/255
          Color colors = new Color(color.r, color.g, color.b,  255f);

        // Restore button color after the delay
        ChangeButtonColor(button, colors);

        // If the button is the correct answer button, find its index and restore its color
        int correctAnswerIndex = answers.FindIndex(answers => answers.isCorrect);
        if (correctAnswerIndex != -1)
        {
            ChangeButtonColor(answerButtons[correctAnswerIndex], color);
            
        }
    //    ToggleButtonActivation(false);
        yield return new WaitForSeconds(0f);
    }
    public void DeleteElement()
    {
        if (elementWantToDelete != null)
        {
            Destroy(elementWantToDelete);
        }

    }
    public void SceneReLoad()
    {
        nextAction.Invoke();
       // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void SceneLoading()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
