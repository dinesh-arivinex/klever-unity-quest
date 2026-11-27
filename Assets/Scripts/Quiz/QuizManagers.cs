using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

public class QuizManagers : MonoBehaviour
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
    public UnityEvent EndEvent;
    public GameObject elementWantToDelete;
    private int MCQstudetScore = 0;
    private GamificationManager gamificationManager;
    public List<InteractableUnityEventWrapper> interactableWrappers = new List<InteractableUnityEventWrapper>();
    public AudioSource audioSource;

    public GameObject[] questionsFillIt, answersFillIt, answerPlacedTextFillIt;
    public GameObject[] questionsMatchIt, answersMatchIt, answerPlacedTextMatchIt;
    [HideInInspector]
    public int answerPlacedCount;
    [HideInInspector]
    public int totQuestionFillItCount;
    [HideInInspector]
    public int totQuestionMatchItCount;

    private bool hasDisplayedAllFillInTheBlanks = false;

    public FillItReferenceManager fillItReferenceManager;
    public MatchItReferenceManager matchItReferenceManager;


    private Image targetImage;
    private Color correctColor = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
    private Color wrongColor = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

    public ColliderTriggerMatch colliderTriggerMatch;
    [System.Serializable]
    private class Wrapper
    {
        public List<QuestionData> questions;
    }

    [System.Serializable]
    private struct QuestionData
    {
        public string question;
        public List<AnswerData> options;
        public List<AnswerData> answer;
        public string createdBy;
        public string sessionId;
        public string id;
        public string typeOfGame;
        public string questionImageUrl;
    }


    public class Option
    {
        public string text;
        public bool isCorrect;
        public string id;
    }
    // private List<QuestionData> questions;
    private int currentQuestionIndex = 0;
    //public MeshRenderer[] meshRender;
    public List<Option> options;
    private List<AnswerData> answers = new List<AnswerData>();
    private void OnEnable()
    {
        gamificationManager = FindAnyObjectByType<GamificationManager>();
        apiManager = FindAnyObjectByType<API>();
        //Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"questions\":" + apiManager.assessmentsString + "}");
        //questions = wrapper.questions;

        //   DisplayQuestion();
        StartEvent.Invoke();
        nextButton.enabled = false;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = false;
        Invoke("DeleteElement", 4f);
        //foreach (var element in meshRender) 
        //{
        //    element.enabled = true;
        //}

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            NextButtonClick();
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            MatchSubmit();
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            NextButtonClickFillIt();
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            NextButtonClickFillIt();
        }
    }

    private void Start()
    {
        answerPlacedCount = 0;
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
                this.gameObject.SetActive(true);
                StartCoroutine(LoadImageCoroutine(url, texture => {
                    rawImg.texture = texture;
                }));
            }
        }
    }

    private IEnumerator LoadImageCoroutine(string url, Action<Texture2D> onSuccess)
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

    private void SetMatchItImage(GameObject targetGo, TextMeshProUGUI textComp, string url)
    {
        if (targetGo == null) return;

        // If targetGo is the text object itself, attach image to its parent so it renders behind the text as a sibling
        Transform attachParent = targetGo.transform;
        int targetSiblingIndex = 0;

        if (targetGo.GetComponent<TextMeshProUGUI>() != null && targetGo.transform.parent != null)
        {
            attachParent = targetGo.transform.parent;
            // Set to 0 so it renders as the very first child (behind the button background and text)
            targetSiblingIndex = 0;
        }

        Transform existing = attachParent.Find("DynamicMatchItImage");
        RawImage rawImg = null;
        if (existing != null)
        {
            rawImg = existing.GetComponent<RawImage>();
        }
        else
        {
            GameObject imgGo = new GameObject("DynamicMatchItImage");
            imgGo.transform.SetParent(attachParent, false);
            imgGo.transform.SetSiblingIndex(targetSiblingIndex); // Draw behind text
            rawImg = imgGo.AddComponent<RawImage>();

            RectTransform rect = imgGo.GetComponent<RectTransform>();
            // Stretch to fill the parent perfectly
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(0, 0);
            rect.offsetMax = new Vector2(0, 0);
            rect.sizeDelta = Vector2.zero; // sizeDelta should be 0 when anchors are 0 and 1
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
                this.gameObject.SetActive(true); // Ensure GameObject is active before starting coroutine

                StartCoroutine(LoadImageCoroutine(url, texture => {
                    rawImg.texture = texture;
                }));
            }
        }
    }

    public void DisplayAllFillInTheBlanks(List<string> allQuestions, List<List<AnswerData>> allAnswersList, List<string> typeOfGames)
    {
        if (hasDisplayedAllFillInTheBlanks) return;

        // Set the flag to true so it never runs again
        hasDisplayedAllFillInTheBlanks = true;
        try
        {
            DebugUtil.Log($"[QuizManagers] DisplayAllFillInTheBlanks called with {allQuestions?.Count} questions.");
            if (allQuestions == null) return;
            totQuestionFillItCount = 0;
            for (int i = 0; i < allQuestions.Count && typeOfGames != null && i < typeOfGames.Count; i++)
                if (typeOfGames[i] == "FillInTheBlanks") totQuestionFillItCount++;

            GameObject[] questions = null;
            GameObject[] answers = null;
            GameObject[] answersPlacement = null;

            if (fillItReferenceManager != null)
            {
                questions = fillItReferenceManager.Questions;
                answers = fillItReferenceManager.Answer;
                answersPlacement = fillItReferenceManager.AnswerPlacement;
            }
            else
            {
                DebugUtil.LogError("[QuizManagers] FillItReferenceManager.Instance is null! Cannot display FillInTheBlanks.");
                return;
            }

            SetFillInRows(questions, totQuestionFillItCount);
            SetFillInRows(answers, totQuestionFillItCount);
            SetFillInRows(answersPlacement, totQuestionFillItCount);
            List<string> shuffledAnswers = new List<string>();
            for (int i = 0; i < allQuestions.Count; i++)
            {
                if (typeOfGames != null && i < typeOfGames.Count && typeOfGames[i] == "FillInTheBlanks")
                {
                    if (allAnswersList != null && i < allAnswersList.Count && allAnswersList[i] != null && allAnswersList[i].Count > 0)
                        shuffledAnswers.Add(allAnswersList[i][0].text);
                    else
                        shuffledAnswers.Add("");
                }
            }
            for (int i = 0; i < shuffledAnswers.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(0, shuffledAnswers.Count);
                string temp = shuffledAnswers[i];
                shuffledAnswers[i] = shuffledAnswers[rnd];
                shuffledAnswers[rnd] = temp;
            }

            int uiIndex = 0;
            for (int i = 0; i < allQuestions.Count; i++)
            {
                if (typeOfGames != null && i < typeOfGames.Count && typeOfGames[i] == "FillInTheBlanks")
                {
                    DebugUtil.Log($"[QuizManagers] Found FillInTheBlanks at index {i}. Target uiIndex={uiIndex}");

                    if (questionsFillIt != null)
                    {
                        if (uiIndex < questionsFillIt.Length)
                        {
                            if (questionsFillIt[uiIndex] != null)
                            {
                                questionsFillIt[uiIndex].GetComponent<TextMeshProUGUI>().text = allQuestions[i];
                                DebugUtil.Log($"[QuizManagers] Success assigning Question to uiIndex {uiIndex}: {allQuestions[i]}");
                            }
                            else DebugUtil.Log($"[QuizManagers] FAILED Question at uiIndex {uiIndex}: questionsFillIt[{uiIndex}] is NULL (not assigned in Inspector).");
                        }
                        else DebugUtil.Log($"[QuizManagers] FAILED Question at uiIndex {uiIndex}: questionsFillIt array length is only {questionsFillIt.Length}!");
                    }

                    if (allAnswersList != null && i < allAnswersList.Count && allAnswersList[i] != null && allAnswersList[i].Count > 0)
                    {
                        if (answersFillIt != null && uiIndex < answersFillIt.Length && answersFillIt[uiIndex] != null)
                        {
                            var tmp = answersFillIt[uiIndex].GetComponent<TextMeshProUGUI>();
                            if (tmp != null)
                            {
                                tmp.text = shuffledAnswers[uiIndex];
                            }
                            else DebugUtil.Log($"[QuizManagers] FAILED Answer at uiIndex {uiIndex}: missing TMP component.");
                        }
                        else DebugUtil.Log($"[QuizManagers] FAILED Answer at uiIndex {uiIndex}: answersFillIt[{uiIndex}] is missing or out of bounds.");

                        if (answerPlacedTextFillIt != null && uiIndex < answerPlacedTextFillIt.Length && answerPlacedTextFillIt[uiIndex] != null)
                        {
                            var tmp = answerPlacedTextFillIt[uiIndex].GetComponent<TextMeshProUGUI>();
                            if (tmp != null)
                            {
                                tmp.text = allAnswersList[i][0].text;
                            }
                            else DebugUtil.Log($"[QuizManagers] FAILED AnswerPlacedText at uiIndex {uiIndex}: missing TMP component.");
                        }
                        else DebugUtil.Log($"[QuizManagers] FAILED AnswerPlacedText at uiIndex {uiIndex}: missing or out of bounds.");
                    }
                    else DebugUtil.Log($"[QuizManagers] NO ANSWER DATA for question {i}");

                    uiIndex++;
                }
            }
        }
        catch (System.Exception e)
        {
            DebugUtil.LogError($"[QuizManagers] ERROR in DisplayAllFillInTheBlanks: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void SetFillInRows(GameObject[] rows, int count)
    {
        for (int i = 0; i < rows.Length; i++)
            if (rows[i] != null) rows[i].SetActive(i < count);
    }

    private bool hasDisplayedAllMatchIt = false;

    public void DisplayAllMatchIt(List<AnswerData> matchingOptions)
    {
        matchingSubmitted = false;
        if (hasDisplayedAllMatchIt) return;
        hasDisplayedAllMatchIt = true;

        try
        {
            if (matchingOptions == null || matchingOptions.Count == 0) return;
            totQuestionMatchItCount = matchingOptions.Count;

            GameObject[] questions = null;
            GameObject[] answers = null;
            GameObject[] answeTextStore = null;

            if (matchItReferenceManager != null)
            {
                questions = matchItReferenceManager.Questions;
                answers = matchItReferenceManager.Answer;
                answeTextStore = matchItReferenceManager.AnsweTextStore;
            }
            else
            {
                DebugUtil.LogError("[QuizManagers] matchItReferenceManager is null! Cannot display MatchIt.");
                return;
            }

            for (int i = 0; i < questions.Length; i++)
            {
                bool active = i < totQuestionMatchItCount;
                if (questions[i] != null) questions[i].SetActive(active);
                if (answers[i] != null) answers[i].SetActive(active);
                if (answeTextStore[i] != null) answeTextStore[i].SetActive(active);
            }

            List<string> rightTexts = new List<string>();
            List<string> rightImageUrls = new List<string>();
            for (int i = 0; i < totQuestionMatchItCount; i++)
            {
                string leftText = matchingOptions[i].left.text;
                string rightText = matchingOptions[i].right.text;
                string leftImageUrl = matchingOptions[i].left.imageUrl;
                string rightImageUrl = matchingOptions[i].right.imageUrl;

                // left text one by one orderwise in questions array
                if (questions[i] != null)
                {
                    TextMeshProUGUI textComp = questions[i].GetComponent<TextMeshProUGUI>();
                    if (textComp == null) textComp = questions[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (textComp != null) textComp.text = leftText;

                    SetMatchItImage(questions[i], textComp, leftImageUrl);
                }

                // store orderwise answertext for checking answer after drag and drop
                if (answeTextStore[i] != null)
                {
                    TextMeshProUGUI textComp = answeTextStore[i].GetComponent<TextMeshProUGUI>();
                    if (textComp == null) textComp = answeTextStore[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (textComp != null) textComp.text = rightText;
                }

                if (answerPlacedTextMatchIt != null && i < answerPlacedTextMatchIt.Length && answerPlacedTextMatchIt[i] != null)
                {
                    TextMeshProUGUI textComp = answerPlacedTextMatchIt[i].GetComponent<TextMeshProUGUI>();
                    if (textComp == null) textComp = answerPlacedTextMatchIt[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (textComp != null) textComp.text = rightText;
                }

                rightTexts.Add(rightText);
                rightImageUrls.Add(rightImageUrl);
            }

            // Shuffle rightTexts randomly but ensure they are not in original position
            if (rightTexts.Count > 1)
            {
                List<string> originalRightTexts = new List<string>(rightTexts);
                System.Random rng = new System.Random();
                bool isDerangement = false;
                int attempts = 0;

                while (!isDerangement && attempts < 100)
                {
                    attempts++;
                    // Fisher-Yates shuffle
                    int n = rightTexts.Count;
                    while (n > 1)
                    {
                        n--;
                        int k = rng.Next(n + 1);
                        string value = rightTexts[k];
                        rightTexts[k] = rightTexts[n];
                        rightTexts[n] = value;

                        string urlValue = rightImageUrls[k];
                        rightImageUrls[k] = rightImageUrls[n];
                        rightImageUrls[n] = urlValue;
                    }

                    // Check if it's a derangement (no element in its original index)
                    isDerangement = true;
                    for (int i = 0; i < rightTexts.Count; i++)
                    {
                        if (rightTexts[i] == originalRightTexts[i])
                        {
                            isDerangement = false;
                            break;
                        }
                    }
                }
            }

            // answer array store right text randomly not orderwise
            for (int i = 0; i < totQuestionMatchItCount; i++)
            {
                if (answers[i] != null)
                {
                    TextMeshProUGUI textComp = answers[i].GetComponent<TextMeshProUGUI>();
                    if (textComp == null) textComp = answers[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (textComp != null) textComp.text = rightTexts[i];

                    SetMatchItImage(answers[i], textComp, rightImageUrls[i]);
                }
            }
        }
        catch (System.Exception e)
        {
            DebugUtil.LogError($"[QuizManagers] ERROR in DisplayAllMatchIt: {e.Message}\n{e.StackTrace}");
        }
    }

    public void DisplayQuestion(List<string> heading, List<AnswerData> answerOptions, string actualQuestion, string questionImageUrl, string typeOfGame = "", List<AnswerData> answerList = null)
    {
        DebugUtil.Log($"[QuizManagers] DisplayQuestion called. typeOfGame: '{typeOfGame}', question: '{actualQuestion}'");
        if (answerList != null)
        {
            DebugUtil.Log($"[QuizManagers] answerList count: {answerList.Count}");
        }
        else
        {
            DebugUtil.Log("[QuizManagers] answerList is NULL");
        }

        if (typeOfGame == "FillInTheBlanks" && answerOptions != null)
        {
            for (int i = 0; i < answerOptions.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(0, answerOptions.Count);
                AnswerData temp = answerOptions[i];
                answerOptions[i] = answerOptions[rnd];
                answerOptions[rnd] = temp;
            }
        }

        answers = answerOptions;
        questionText.text = actualQuestion;
        DisplayQuestionImage(questionImageUrl);

        if (questionText != null && currentQuestionIndex < 1)
        {
            // MCQquestionText.text = $"MCQ{currentQuestionIndex + 1}";
            MCQquestionText.text = heading[0];
            // questionPanelText.text = $"Question{currentQuestionIndex + 1}";
            questionPanelText.text = heading[1];
            //questionText.text = $"{questions[currentQuestionIndex].question}";

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
            var liveResponse = new SimpleJSON.JSONObject();
            liveResponse["selectedIndex"] = selectedAnswerIndex;
            liveResponse["selectedText"] = selectedAnswerIndex >= 0 && selectedAnswerIndex < answers.Count ? answers[selectedAnswerIndex].text : string.Empty;
            liveResponse["selectedOptionId"] = selectedAnswerIndex >= 0 && selectedAnswerIndex < answers.Count ? (answers[selectedAnswerIndex]._id ?? string.Empty) : string.Empty;
            liveResponse["isCorrect"] = selectedAnswerIndex >= 0 && selectedAnswerIndex < answers.Count && answers[selectedAnswerIndex].isCorrect;
            Klass.Net.LiveViewPublisher.Instance?.PublishAssessmentInteraction(gamificationManager.gameCount, liveResponse, true);
            Button clickedButton = answerButtons[selectedAnswerIndex];
            // Create colors with adjusted alpha values
            Color greenWithAlpha = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
            Color redWithAlpha = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);
            
            // Change button colors for all options
            for (int i = 0; i < answers.Count; i++)
            {
                if (answerButtons[i] != null && answerButtons[i].gameObject.activeSelf)
                {
                    ChangeButtonColor(answerButtons[i], answers[i].isCorrect ? greenWithAlpha : redWithAlpha);
                }
            }

            if (answers[selectedAnswerIndex].isCorrect)
            {
                // MCQstudetScore++;
                gamificationManager.mcqScore++;
            }
            buttonClicked = true;
            ToggleButtonActivation(false);
            audioSource.Play();
        }
        nextButton.enabled = true;
        nextButton.interactable = true;
        var nextCollider = nextButton.GetComponent<BoxCollider>();
        if (nextCollider != null) nextCollider.enabled = true;
    }

    public void ToggleButtonActivation(bool activated)
    {
        foreach (var button in answerButtons)
        {
            button.enabled = activated;
        }
    }

    private void NextButtonClick()
    {
        nextButton.enabled = false;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = false;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            Color newColor = new Color(150f / 255f, 150f / 255f, 150f / 255f, 255f / 255f);
            StartCoroutine(RestoreButtonColor(answerButtons[i], newColor));
        }
        ToggleButtonActivation(true);
        buttonClicked = false;

        currentQuestionIndex++;
        if (currentQuestionIndex < 1)
        {
            // DisplayQuestion();
        }
        else
        {
            reloadSceneEvent.Invoke();
            DebugUtil.Log("Your score is" + MCQstudetScore);
            gamificationManager.OnGameCompleted();


        }
    }

    public void NextButtonClickFillIt()
    {
        if (LessonPause.IsPaused) return;
        nextButton.enabled = false;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = false;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            Color newColor = new Color(150f / 255f, 150f / 255f, 150f / 255f, 255f / 255f);
            StartCoroutine(RestoreButtonColor(answerButtons[i], newColor));
        }
        ToggleButtonActivation(true);
        buttonClicked = false;
        reloadSceneEvent.Invoke();
        DebugUtil.Log("Your score is" + MCQstudetScore);
        gamificationManager.OnGameCompleted();


    }

    public void NextButtonClickMatchIt()
    {
        nextButton.enabled = false;
        nextButton.gameObject.GetComponent<BoxCollider>().enabled = false;
        ToggleButtonActivation(true);
        buttonClicked = false;
        reloadSceneEvent.Invoke();
        DebugUtil.Log("Your score is" + MCQstudetScore);
        gamificationManager.OnGameCompleted();


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
        // Color newColorWithAlpha = new Color(color.r, color.g, color.b, 95f / 255f);

        // Restore button color after the delay
        ChangeButtonColor(button, color);

        // If the button is the correct answer button, find its index and restore its color
        int correctAnswerIndex = answers.FindIndex(answers => answers.isCorrect);
        if (correctAnswerIndex != -1)
        {
            ChangeButtonColor(answerButtons[correctAnswerIndex], color);
        }
        yield return new WaitForSeconds(0f);
    }
    public void DeleteElement()
    {
        Destroy(elementWantToDelete);
    }
    public void SceneReLoad()
    {
        EndEvent.Invoke();
        // SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void DisableImagesInArray(GameObject[] arr)
    {
        if (arr == null) return;
        foreach (var obj in arr)
        {
            if (obj == null) continue;
            Transform img = obj.transform.Find("DynamicMatchItImage");
            if (img != null) img.gameObject.SetActive(false);

            if (obj.transform.parent != null)
            {
                img = obj.transform.parent.Find("DynamicMatchItImage");
                if (img != null) img.gameObject.SetActive(false);
            }
        }
    }

    public void MatchSubmit()
    {
        if (LessonPause.IsPaused || matchingSubmitted) return;
        matchingSubmitted = true;
        if (gamificationManager == null)
            gamificationManager = FindAnyObjectByType<GamificationManager>(FindObjectsInactive.Include);
        if (matchItReferenceManager != null)
        {
            DisableImagesInArray(matchItReferenceManager.Questions);
            DisableImagesInArray(matchItReferenceManager.Answer);
        }

        colliderTriggerMatch.Answer1HandGrab.gameObject.SetActive(false);
        colliderTriggerMatch.Answer5HandGrab.gameObject.SetActive(false);
        colliderTriggerMatch.Answer2HandGrab.gameObject.SetActive(false);
        colliderTriggerMatch.Answer3HandGrab.gameObject.SetActive(false);
        colliderTriggerMatch.Answer4HandGrab.gameObject.SetActive(false);

        int matchCount = totQuestionMatchItCount > 0
            ? Mathf.Min(totQuestionMatchItCount, answersMatchIt.Length, answerPlacedTextMatchIt.Length)
            : Mathf.Min(answersMatchIt.Length, answerPlacedTextMatchIt.Length);

        for (int i = 0; i < matchCount; i++)
        {
            if (answersMatchIt[i] == null || answerPlacedTextMatchIt[i] == null) continue;
            var answerTmp = answersMatchIt[i].GetComponent<TextMeshProUGUI>();
            var expectedTmp = answerPlacedTextMatchIt[i].GetComponent<TextMeshProUGUI>();
            if (answerTmp == null || expectedTmp == null) continue;

            targetImage = answersMatchIt[i].transform.parent != null
                ? answersMatchIt[i].transform.parent.GetComponent<Image>()
                : null;
            bool correct = !string.IsNullOrWhiteSpace(answerTmp.text) && answerTmp.text == expectedTmp.text;
            if (!string.IsNullOrWhiteSpace(answerTmp.text) && !string.IsNullOrWhiteSpace(expectedTmp.text))
                RecordResponse(i, answerTmp.text, correct);
            if (correct)
            {
                ApplyColor(correctColor, targetImage);
                if (gamificationManager != null) gamificationManager.mcqScore++;
            }
            else
            {
                ApplyColor(wrongColor, targetImage);
            }
        }

        StartCoroutine(NextEnable());
    }

    private int FindMatchItAnswerIndex(GameObject obj)
    {
        if (answersMatchIt == null) return -1;
        for (int i = 0; i < answersMatchIt.Length; i++)
        {
            if (answersMatchIt[i] == null) continue;
            // Check if answersMatchIt[i] is the exact object or any child within the obj's hierarchy
            if (answersMatchIt[i] == obj || answersMatchIt[i].transform.IsChildOf(obj.transform))
            {
                return i;
            }
        }
        return -1;
    }

    public void SwapMatchItAnswers(GameObject objA, GameObject objB)
    {
        if (answersMatchIt == null) return;

        int indexA = FindMatchItAnswerIndex(objA);
        int indexB = FindMatchItAnswerIndex(objB);

        if (indexA != -1 && indexB != -1)
        {
            // 1. Swap GameObject array order (sibling index)
            int siblingIndexA = objA.transform.GetSiblingIndex();
            int siblingIndexB = objB.transform.GetSiblingIndex();

            if (siblingIndexA < siblingIndexB)
            {
                objB.transform.SetSiblingIndex(siblingIndexA);
                objA.transform.SetSiblingIndex(siblingIndexB);
            }
            else if (siblingIndexB < siblingIndexA)
            {
                objA.transform.SetSiblingIndex(siblingIndexB);
                objB.transform.SetSiblingIndex(siblingIndexA);
            }

            // 2. Swap array elements
            GameObject temp = answersMatchIt[indexA];
            answersMatchIt[indexA] = answersMatchIt[indexB];
            answersMatchIt[indexB] = temp;

            DebugUtil.Log($"Swapped array elements at indices {indexA} and {indexB}");
        }
        else
        {
            DebugUtil.Log($"SwapMatchItAnswers failed to find objects in array: indexA={indexA}, indexB={indexB}");
        }
    }

    public void MoveMatchItAnswerToSlot(GameObject snappedObject, int newSlotIndex)
    {
        if (answersMatchIt == null || newSlotIndex < 0 || newSlotIndex >= answersMatchIt.Length) return;

        int currentIndex = System.Array.IndexOf(answersMatchIt, snappedObject);
        if (currentIndex != -1 && currentIndex != newSlotIndex)
        {
            // Swap with whatever is currently in the new slot
            GameObject objectInNewSlot = answersMatchIt[newSlotIndex];
            SwapMatchItAnswers(snappedObject, objectInNewSlot);
        }
    }

    private bool matchingSubmitted;
    public bool IsMatchSubmitted => matchingSubmitted;
    public void RecordFillAnswer(TextMeshProUGUI expected, string selected)
    {
        int index = System.Array.FindIndex(answerPlacedTextFillIt, obj => obj == expected.gameObject);
        if (index >= 0 && !string.IsNullOrWhiteSpace(selected)) RecordResponse(index, selected, selected == expected.text);
    }
    private void RecordResponse(int index, string selected, bool correct)
    {
        var response = new SimpleJSON.JSONObject();
        response["selectedText"] = selected;
        response["isCorrect"] = correct;
        Klass.Net.LiveViewPublisher.Instance?.PublishAssessmentInteraction(index, response, true);
    }
    public IEnumerator NextEnable()
    {
        yield return new WaitForSeconds(2f);
        nextButton.gameObject.SetActive(true);
        nextButton.enabled = true;
        nextButton.interactable = true;
        var nextCollider = nextButton.GetComponent<BoxCollider>();
        if (nextCollider != null) nextCollider.enabled = true;
    }
    private void ApplyColor(Color color, Image Image)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }
}
