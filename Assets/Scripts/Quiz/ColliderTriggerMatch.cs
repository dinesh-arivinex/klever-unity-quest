using UnityEngine;
using Oculus.Interaction;
using System.Collections;

public class ColliderTriggerMatch : MonoBehaviour
{
    public GameObject Answer1Pos, Answer2Pos, Answer3Pos, Answer4Pos, Answer5Pos;
    public GameObject Answer1HandGrab, Answer2HandGrab, Answer3HandGrab, Answer4HandGrab, Answer5HandGrab;

    // We store the current "slot" or resting position of each answer here.
    // Made public so all instances can sync with each other
    [HideInInspector] public Vector3 pos1, pos2, pos3, pos4, pos5;

    private QuizManagers quizManager;

    private void Start()
    {
        quizManager = GetComponentInParent<QuizManagers>(true) ?? FindAnyObjectByType<QuizManagers>();

        // Store the actual Vector3 positions, NOT the transform references!
        if (Answer1Pos != null) pos1 = Answer1Pos.transform.position;
        if (Answer2Pos != null) pos2 = Answer2Pos.transform.position;
        if (Answer3Pos != null) pos3 = Answer3Pos.transform.position;
        if (Answer4Pos != null) pos4 = Answer4Pos.transform.position;
        if (Answer5Pos != null) pos5 = Answer5Pos.transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (LessonPause.IsPaused || (quizManager != null && quizManager.IsMatchSubmitted)) return;

        // Check if the collision is between two answers
        if (IsAnswer(gameObject) && IsAnswer(other.gameObject))
        {
            // Prevent double-swapping! Since both colliders will trigger OnTriggerEnter,
            // we use GetInstanceID() to ensure the swap logic only runs ONCE per collision.
            if (gameObject.GetInstanceID() < other.gameObject.GetInstanceID())
            {
                SwapPositions(gameObject, other.gameObject);
            }
        }
    }

    private bool IsAnswer(GameObject obj)
    {
        return obj == Answer1Pos || obj == Answer2Pos || obj == Answer3Pos || obj == Answer4Pos || obj == Answer5Pos;
    }

    private void SwapPositions(GameObject objA, GameObject objB)
    {
        // 1. Get their current slot positions
        Vector3 slotA = GetCurrentSlotPosition(objA);
        Vector3 slotB = GetCurrentSlotPosition(objB);

        // 2. Move objects to each other's slot position
        objA.transform.position = slotB;
        objB.transform.position = slotA;

        // 3. Update our tracked slot positions so they can be swapped again later if needed
        UpdateSlotPosition(objA, slotB);
        UpdateSlotPosition(objB, slotA);

        if (quizManager != null)
        {
            quizManager.SwapMatchItAnswers(objA, objB);
        }

        // 4. Start Coroutine to disable and re-enable colliders and grab
        StartCoroutine(DisableAndReenableRoutine(objA, objB));
        
        Debug.Log($"Swapped {objA.name} and {objB.name}");
    }

    private IEnumerator DisableAndReenableRoutine(GameObject objA, GameObject objB)
    {
        // Disable them so they drop and don't re-collide instantly
        objA.GetComponent<BoxCollider>().enabled = false;
        objA.GetComponent<Grabbable>().enabled = false;
        objB.GetComponent<BoxCollider>().enabled = false;
        objB.GetComponent<Grabbable>().enabled = false;

        DisableHandGrab(objA);
        DisableHandGrab(objB);

        // Wait for 3 seconds BEFORE executing the lines below
        yield return new WaitForSeconds(3f);

        // Re-enable them
        objA.GetComponent<BoxCollider>().enabled = true;
        objA.GetComponent<Grabbable>().enabled = true;
        objB.GetComponent<BoxCollider>().enabled = true;
        objB.GetComponent<Grabbable>().enabled = true;

        EnableHandGrab(objA);
        EnableHandGrab(objB);
    }

    private Vector3 GetCurrentSlotPosition(GameObject obj)
    {
        if (obj == Answer1Pos) return pos1;
        if (obj == Answer2Pos) return pos2;
        if (obj == Answer3Pos) return pos3;
        if (obj == Answer4Pos) return pos4;
        if (obj == Answer5Pos) return pos5;
        return obj.transform.position;
    }

    private void UpdateSlotPosition(GameObject obj, Vector3 newPos)
    {
        // Update this instance's slots
        UpdateLocalSlot(obj, newPos);

        // Because this script is attached to EACH answer, we must update the slots
        // on all other instances too, otherwise they get out of sync on the 2nd/3rd swap!
        ColliderTriggerMatch[] allMatches = FindObjectsOfType<ColliderTriggerMatch>();
        foreach (var match in allMatches)
        {
            if (match != this) 
            {
                match.UpdateLocalSlot(obj, newPos);
            }
        }
    }

    private void UpdateLocalSlot(GameObject obj, Vector3 newPos)
    {
        if (obj == Answer1Pos) pos1 = newPos;
        else if (obj == Answer2Pos) pos2 = newPos;
        else if (obj == Answer3Pos) pos3 = newPos;
        else if (obj == Answer4Pos) pos4 = newPos;
        else if (obj == Answer5Pos) pos5 = newPos;
    }

    private void DisableHandGrab(GameObject obj)
    {
        if (obj == Answer1Pos && Answer1HandGrab != null) Answer1HandGrab.SetActive(false);
        if (obj == Answer2Pos && Answer2HandGrab != null) Answer2HandGrab.SetActive(false);
        if (obj == Answer3Pos && Answer3HandGrab != null) Answer3HandGrab.SetActive(false);
        if (obj == Answer4Pos && Answer4HandGrab != null) Answer4HandGrab.SetActive(false);
        if (obj == Answer5Pos && Answer5HandGrab != null) Answer5HandGrab.SetActive(false);
    }

    private void EnableHandGrab(GameObject obj)
    {
        if (obj == Answer1Pos && Answer1HandGrab != null) Answer1HandGrab.SetActive(true);
        if (obj == Answer2Pos && Answer2HandGrab != null) Answer2HandGrab.SetActive(true);
        if (obj == Answer3Pos && Answer3HandGrab != null) Answer3HandGrab.SetActive(true);
        if (obj == Answer4Pos && Answer4HandGrab != null) Answer4HandGrab.SetActive(true);
        if (obj == Answer5Pos && Answer5HandGrab != null) Answer5HandGrab.SetActive(true);
    }
}
