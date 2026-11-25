using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GamificationController : MonoBehaviour
{

    public bool isBasketBall, isArchery;
    public static bool isGamefication;
    public Button[] nextButton;
    public GameObject startPoint, startPointHandMode;
    public GameObject startpoint_A, startpoint_AHandMode;
    public GameObject rightHand, rightController;
    public Button[] answerButton;
    public Button buttonBasketBall;
    public Button buttonArrow;
    public AudioSource audioSource;
    public GameObject ballPrefab, ballPrefabHandMode;
    public GameObject arrowPrefab, arrowPrefabHandMode;
    public GameObject arrowVisualize;
    private int gameNumber;
    private GamificationManager gamificationManager;
    public BoxCollider[] boxColliders;
    public bool arrowstatus;
    Transform startingTransform;
    void Start()
    {
        gamificationManager = GameManager.Instance.gamificationManager;
        gameNumber = 1;
        startingTransform = startpoint_AHandMode != null ? startpoint_AHandMode.transform : null;
    }
    public void ToggleCollider(bool isTrue)
    {
        //foreach (BoxCollider item in boxColliders)
        //{
        //    item.enabled = isTrue;  
        //}
        arrowstatus = !isTrue;

    }
    public void ActivateArrowStatus()
    {
        ToggleCollider(false);
    }
    public void EnbleClick()
    {
        SetParent();
        //StartCoroutine(EnableNextButton());
    }
    public IEnumerator EnableNextButton()
    {
        yield return new WaitForSeconds(2f);

        nextButton[0].enabled = true;
        nextButton[1].enabled = true;

    }
    public void EnableGamification()
    {
        isGamefication = true;
    }
    public void GameController(int num)
    {
        gameNumber = num;
        if (num == 1)
        {
            isBasketBall = true;
            isArchery = false;
        }
        else
        {
            isBasketBall = false;
            isArchery = true;
        }


    }
    public void UnchildStartPoint()
    {
        if (gamificationManager.currentGameName == "Basketball")
        {
            ballPrefab.SetActive(false);
        }
        else
        {
            arrowPrefab.SetActive(false);
            //  arrowVisualize.SetActive(false);
        }
        StartCoroutine(ParentUnchildDelay());

    }
    private IEnumerator ParentUnchildDelay()
    {
        yield return new WaitForSeconds(.2f);
        //if (!GamificationControllerHandSwitcher.Instance.isHandMode)
        //{
        //    startPoint.transform.SetParent(null);
        //    startpoint_A.transform.SetParent(null);
        //}
        //else
        //{
        //    startPointHandMode.transform.SetParent(null);
        //    startpoint_AHandMode.transform.SetParent(null);
        //}
    }
    public void SetParent()
    {
        //if (!GamificationControllerHandSwitcher.Instance.isHandMode)
        //{
        //    startPoint.transform.SetParent(rightController.transform);
        //    startpoint_A.transform.SetParent(rightController.transform);
        //}
        //else
        //{
        //    startPointHandMode.transform.SetParent(rightHand.transform);
        //    startpoint_AHandMode.transform.SetParent(rightHand.transform);
        //    startpoint_AHandMode.transform.localPosition = startingTransform.localPosition;
        //    startpoint_AHandMode.transform.localRotation = startingTransform.localRotation;
        //}
    }

    public void EnableBasketBallButton()
    {
        audioSource.Play();
        buttonBasketBall.enabled = true;

    }
    public void EnableArrowButton()
    {
        audioSource.Play();
        buttonArrow.enabled = true;


    }
}
