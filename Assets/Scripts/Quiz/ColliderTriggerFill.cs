using System.Collections;
using System.Collections.Generic;
using LibTessDotNet;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

public class ColliderTriggerFill : MonoBehaviour
{
    public GameObject Answer1,Answer2, Answer3, Answer4,Answer5;
    public GameObject PositionGetter;
    public TextMeshProUGUI textfromRes;
    private Image targetImage;
    private Color correctColor = new Color(Color.green.r, Color.green.g, Color.green.b, 95f / 255f);
    private Color wrongColor = new Color(Color.red.r, Color.red.g, Color.red.b, 255f / 255f);

    public QuizManagers fillItQuizManagers;
    private GamificationManager gamificationManager;

    private void Start()
    {
        gamificationManager = FindAnyObjectByType<GamificationManager>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (LessonPause.IsPaused) return;

        if (other.gameObject == Answer1)
        {
            GameObject grandParent = Answer1.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer1.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer1.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            targetImage = Answer1.GetComponent<Image>();

            this.GetComponent<BoxCollider>().enabled = false;
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer1.GetComponent<BoxCollider>().enabled = false;
            grandParent.GetComponent<Grabbable>().enabled = false;
            grandParent.GetComponent<SnapInteractor>().enabled = false; 
            grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                //this.GetComponent<BoxCollider>().enabled = false;
                //this.gameObject.GetComponent<Image>().enabled = false;
                //Answer1.GetComponent<BoxCollider>().enabled = false;
                //grandParent.GetComponent<Grabbable>().enabled = false;
                //grandParent.GetComponent<SnapInteractor>().enabled = false;
                //grandParent.transform.GetChild(1).gameObject.SetActive(false);
                //StartCoroutine(CallIfWrong());
                //this.GetComponent<BoxCollider>().enabled = true;
                //Answer1.GetComponent<BoxCollider>().enabled = true;
                //grandParent.GetComponent<Grabbable>().enabled = true;
                //grandParent.GetComponent<SnapInteractor>().enabled = true;
                //grandParent.transform.GetChild(1).gameObject.SetActive(true);
                //StartCoroutine(CallIfWrong());
                //this.gameObject.GetComponent<Image>().enabled = false;

                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }
            Answer1.GetComponent<BoxCollider>().enabled = false;
            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
               fillItQuizManagers.nextButton.enabled = true;
               fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer2)
        {
            GameObject grandParent = Answer2.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer2.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer2.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

            targetImage = Answer2.GetComponent<Image>();

            this.GetComponent<BoxCollider>().enabled = false;
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer2.GetComponent<BoxCollider>().enabled = false;
            grandParent.GetComponent<Grabbable>().enabled = false;
            grandParent.GetComponent<SnapInteractor>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                //this.GetComponent<BoxCollider>().enabled = false;
                //Answer2.GetComponent<BoxCollider>().enabled = false;
                //grandParent.GetComponent<Grabbable>().enabled = false;
                //grandParent.GetComponent<SnapInteractor>().enabled = false;
                //grandParent.transform.GetChild(1).gameObject.SetActive(false);
                //StartCoroutine(CallIfWrong());
                //this.GetComponent<BoxCollider>().enabled = true;
                //Answer2.GetComponent<BoxCollider>().enabled = true;
                //grandParent.GetComponent<Grabbable>().enabled = true;
                //grandParent.GetComponent<SnapInteractor>().enabled = true;
                //grandParent.transform.GetChild(1).gameObject.SetActive(true);
                //StartCoroutine(CallIfWrong());
                //this.gameObject.GetComponent<Image>().enabled = false;
                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer2.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer3)
        {
            GameObject grandParent = Answer3.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer3.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer3.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            targetImage = Answer3.GetComponent<Image>();

            this.GetComponent<BoxCollider>().enabled = false;
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer3.GetComponent<BoxCollider>().enabled = false;
            grandParent.GetComponent<Grabbable>().enabled = false;
            grandParent.GetComponent<SnapInteractor>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            { 

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                //this.GetComponent<BoxCollider>().enabled = false;
                //this.gameObject.GetComponent<Image>().enabled = false;
                //Answer3.GetComponent<BoxCollider>().enabled = false;
                //grandParent.GetComponent<Grabbable>().enabled = false;
                //grandParent.GetComponent<SnapInteractor>().enabled = false;
                //grandParent.transform.GetChild(1).gameObject.SetActive(false);
                //StartCoroutine(CallIfWrong());
                //this.GetComponent<BoxCollider>().enabled = true;
                //Answer3.GetComponent<BoxCollider>().enabled = true;
                //grandParent.GetComponent<Grabbable>().enabled = true;
                //grandParent.GetComponent<SnapInteractor>().enabled = true;
                //grandParent.transform.GetChild(1).gameObject.SetActive(true);
                //StartCoroutine(CallIfWrong());
                //this.gameObject.GetComponent<Image>().enabled = false;
                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer3.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer4)
        {
            GameObject grandParent = Answer4.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer4.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer4.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

            this.GetComponent<BoxCollider>().enabled = false;
            targetImage = Answer4.GetComponent<Image>();
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer4.GetComponent<BoxCollider>().enabled = false;
            grandParent.GetComponent<Grabbable>().enabled = false;
            grandParent.GetComponent<SnapInteractor>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor, targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                Debug.Log("Wrong");
                ApplyColor(wrongColor, targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer4.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }

        if (other.gameObject == Answer5)
        {
            GameObject grandParent = Answer5.transform.parent.parent.gameObject;

            Debug.Log(textfromRes.text);
            Debug.Log(Answer5.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text);

            TextMeshProUGUI childText = Answer5.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            this.GetComponent<BoxCollider>().enabled = false;
            targetImage = Answer5.GetComponent<Image>();
            this.gameObject.GetComponent<Image>().enabled = false;
            Answer5.GetComponent<BoxCollider>().enabled = false;
            grandParent.GetComponent<Grabbable>().enabled = false;
            grandParent.GetComponent<SnapInteractor>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<HandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceHandGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.GetComponent<DistanceGrabInteractable>().enabled = false;
            grandParent.transform.GetChild(1).gameObject.SetActive(false);
            fillItQuizManagers.RecordFillAnswer(textfromRes, childText.text);
            if (childText.text == textfromRes.text)
            {

                Debug.Log("Correct");
                ApplyColor(correctColor,targetImage);
                gamificationManager.mcqScore++;
            }
            else
            {
                Debug.Log("Wrong");
                ApplyColor(wrongColor,targetImage);
            }

            grandParent.transform.localPosition = PositionGetter.transform.localPosition;
            Answer5.GetComponent<BoxCollider>().enabled = false;
            fillItQuizManagers.answerPlacedCount++;

            if (fillItQuizManagers.answerPlacedCount == fillItQuizManagers.totQuestionFillItCount)
            {
                fillItQuizManagers.nextButton.enabled = true;
                fillItQuizManagers.nextButton.gameObject.GetComponent<BoxCollider>().enabled = true;
            }
        }
    }

    private void ApplyColor(Color color, Image Image)
    {
        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }

    IEnumerator CallIfWrong()
    {
        yield return new WaitForSeconds(2f);
    }
}
