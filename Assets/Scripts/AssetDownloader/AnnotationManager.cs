using Oculus.Interaction;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using TMPro;
using Unity.VideoHelper;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class AnnotationManager : MonoBehaviour
{
    [System.Serializable]
    public class AnnotationData
    {
        public string UniqueName;
        public string ObjectName;
        public ModelPositionData ModelPosition;
        public RectData RectProperties;
        public string AnnotationText;
        public string annotationCount;
        public Vector3 AnnotationPosition;
        public Quaternion AnnotationRotation;
        public string heading;
    }
    [System.Serializable]
    public class AnnotationPositionData
    {
        public float x;
        public float y;
        public float z;
    }
    [System.Serializable]
    public class AnnotationRotationData
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }
    [System.Serializable]
    public class ModelPositionData
    {
        public Vector3 position;
        public Quaternion rotation;
    }
    [System.Serializable]
    public class RectData
    {
        public float Width;
        public float Height;
        public float PosX;
        public float PosY;
    }
    public GameObject parentObject;
    public GameObject GrabObj;
    public MeshRenderer meshRenderer;
    public string jsonString;
    public GameObject model;
    public Canvas canvasPrefab;
    private Canvas canvas;
    public TextMeshProUGUI textPrefab;
    public float position;
    public Button buttonPrefab;
    public GameObject cubeButton;
    public Transform target;
    private string downlodedModelName;
    public TextMeshProUGUI displayText;
    public TextMeshProUGUI description;
    private GoogleTextToSpeech googleTextToSpeech;
    private LineRenderer lineRender;
    public float distance;
    private List<string> jsonStrings;
    int numberOfAnnotations;
    private int index;
    private List<string> modelTagNames;
    private int modelCount = 0;
    private List<int> orderOfAnnotation;
    private int orderExcicuter;
    private int listexcicuter;
    public GameObject modelDescriptionPanel;
    private GameObject Nmodel;
    private ModelScale modelScale;
    public AssetDownloader assetDownloader;
    private bool isfirstTime;
    public List<Vector3> boundingBoxScales = new List<Vector3>();
    private bool isFirstTime = true;
    private GameObject modelWithcollider;
    public List<GameObject> tempCube = new List<GameObject>();
    // Start is called before the first frame update
    void Start()
    {
        modelScale = GameManager.Instance.modelScale;
        modelTagNames = new List<string>();
        modelTagNames.Add("VRModel");

    }
    public void MultipleModelCall(int value)
    {
        index = value;
        initialization();
    }
    public void initialization()
    {
        parentObject.transform.position = Vector3.zero;
        GrabObj.transform.position = Vector3.zero;
        GameObject objectsWithTag = GameObject.FindGameObjectWithTag(modelTagNames[modelCount]);
        if (objectsWithTag == null) { Debug.LogError("[Model] Expected imported model is unavailable; cannot display this step."); return; }
        Transform childTransform = objectsWithTag.transform.parent;
        model = childTransform.gameObject;
        googleTextToSpeech = FindObjectOfType<GoogleTextToSpeech>();
        //model.transform.SetParent(parentObject.transform);
        GrabObj.GetComponent<BoxCollider>().enabled = true;
        model.transform.SetParent(GrabObj.transform, false);
        Transform child = model.transform.Find("ModelParent");
        model.transform.position = Vector3.zero;
        //The following line sets the model's position to Vector3.zero, ensuring that the second and third models are positioned correctly.

        //  model.transform.position=Vector3.zero;

        if (orderOfAnnotation != null && orderOfAnnotation.Count > listexcicuter)
        {
            if (orderOfAnnotation[listexcicuter] == orderExcicuter)
            {


                AssignPosithionRotationAndCreateCanvas(listexcicuter);
                StartCoroutine(ChangePosition());

                listexcicuter++;

            }
            else
            {
                StartCoroutine(ChangePosition());
            }
        }
        else
        {
            StartCoroutine(ChangePosition());

            //  modelDescriptionPanel.SetActive(false);
        }


        // Placement completes in ChangePosition after imported bounds are ready.
        // SetPosition(); 
        modelCount++;
        modelTagNames.Add("VRModel" + modelCount.ToString());
        orderExcicuter++;

        meshRenderer = model.GetComponentInChildren<MeshRenderer>();
    }
    IEnumerator ChangePosition()
    {
        // this functionn will works when we put annotation without bounding box

        yield return new WaitForSeconds(0.01f);
        Klever.VR.LessonModelPlacement.PlaceOnTable(GrabObj, assetDownloader.instantiationPoint.position);
        var questControls = GrabObj.GetComponent<Klever.VR.QuestLessonModelControls>();
        if (questControls == null) questControls = GrabObj.AddComponent<Klever.VR.QuestLessonModelControls>();
        questControls.Configure(model.transform);
        assetDownloader.PublishVisibleModel();
    }

    Vector3 GetBottomPosition(GameObject obj)
    {
        // Get the bounds of the GameObject
        Bounds bounds;
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
        }
        else
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
            }
            else
            {
                DebugUtil.LogError("GameObject has neither renderer nor collider.");
                return Vector3.zero;
            }
        }

        // Calculate the bottom position
        Vector3 bottomPosition = bounds.center - Vector3.up * bounds.extents.y;

        return bottomPosition;
    }
    public void AssignValueTOJson(List<string> json, List<int> order)
    {
        orderOfAnnotation = order;
        jsonStrings = json;
        numberOfAnnotations = jsonStrings.Count;
    }
    public void AssignPosithionRotationAndCreateCanvas(int index)
    {
        if (jsonStrings == null)
        {
            DebugUtil.LogError("jsonStrings is null or the index is out of range.");
            return;
        }

        // Parse the JSON string into a JSON array


        if (jsonStrings.Count <= index)
        {
            DebugUtil.Log("no element");
            modelDescriptionPanel.SetActive(false);
        }
        else
        {
            modelDescriptionPanel.SetActive(true);
            JSONNode jsonData = JSON.Parse(jsonStrings[index]);
            // Create a list to hold your AnnotationData objects
            List<AnnotationData> data = new List<AnnotationData>();

            // Iterate over each element in the JSON array
            for (int i = 0; i < jsonData.Count; i++)
            {
                JSONNode annotationNode = jsonData[i];

                // Create an AnnotationData object and populate its fields
                AnnotationData annotation = new AnnotationData();

                annotation.UniqueName = annotationNode["UniqueName"];
                annotation.ObjectName = annotationNode["ObjectName"];
                annotation.heading = annotationNode["heading"];
                downlodedModelName = annotation.ObjectName;

                // Parse ModelPositionData
                annotation.ModelPosition = new ModelPositionData
                {
                    position = new Vector3(
                        annotationNode["ModelPosition"]["position"]["x"].AsFloat,
                        annotationNode["ModelPosition"]["position"]["y"].AsFloat,
                        annotationNode["ModelPosition"]["position"]["z"].AsFloat
                    ),
                    rotation = new Quaternion(
                        annotationNode["ModelPosition"]["rotation"]["x"].AsFloat,
                        annotationNode["ModelPosition"]["rotation"]["y"].AsFloat,
                        annotationNode["ModelPosition"]["rotation"]["z"].AsFloat,
                        annotationNode["ModelPosition"]["rotation"]["w"].AsFloat
                    )
                };

                // Parse RectData
                annotation.RectProperties = new RectData
                {
                    Width = annotationNode["RectWidth"].AsFloat,
                    Height = annotationNode["RectHeight"].AsFloat,
                    PosX = annotationNode["RectPosX"].AsFloat,
                    PosY = annotationNode["RectPosY"].AsFloat
                };
                annotation.AnnotationText = annotationNode["AnnotationText"];
                annotation.annotationCount = annotationNode["ObjectCount"];
                annotation.AnnotationPosition = new Vector3(
                annotationNode["AnnotationPosition"]["x"].AsFloat,
                annotationNode["AnnotationPosition"]["y"].AsFloat,
                annotationNode["AnnotationPosition"]["z"].AsFloat
                );

                annotation.AnnotationRotation = new Quaternion(
                annotationNode["AnnotationRotation"]["x"].AsFloat,
                annotationNode["AnnotationRotation"]["y"].AsFloat,
                annotationNode["AnnotationRotation"]["z"].AsFloat,
                annotationNode["AnnotationRotation"]["w"].AsFloat
            );


                // Add the annotation to the list
                data.Add(annotation);


                Vector3 boundingBoxScale = new Vector3(
            annotationNode["boundingboxScale"]["x"].AsFloat,
            annotationNode["boundingboxScale"]["y"].AsFloat,
            annotationNode["boundingboxScale"]["z"].AsFloat
        );

                // Add to HashSet only if it's not repeated
                if (!boundingBoxScales.Contains(boundingBoxScale))
                {
                    boundingBoxScales.Add(boundingBoxScale);
                }
            }

            // Now you have a list of AnnotationData objects
            // You can iterate through the list and do whatever you need to do with the data
            Transform child = model.transform.Find("ModelParent");
            modelWithcollider = child.transform.parent.gameObject;
            modelScale.SetServerValue(child.gameObject);
            canvas = Instantiate(canvasPrefab);
            canvas.tag = "notAChild";
            canvas.renderMode = RenderMode.WorldSpace;

            canvas.transform.SetParent(model.transform.GetChild(0));

            foreach (var annotation in data)
            {
                if (annotation.ObjectName == downlodedModelName)
                {
                    if (isFirstTime)
                    {
                        isFirstTime = false;
                    }

                    RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                    canvasRect.anchoredPosition3D = new Vector3(annotation.RectProperties.PosX, annotation.RectProperties.PosY, 0f);


                    TextMeshProUGUI newText = Instantiate(textPrefab, canvas.transform);
                    newText.text = annotation.AnnotationText;


                    //button
                    Button newButton = Instantiate(buttonPrefab, canvas.transform);
                    newButton.onClick.AddListener(() => OnButtonClick(annotation.AnnotationText, annotation.heading));

                    CreateEmptyObjectAtPosition(new Vector3(annotation.AnnotationPosition.x, annotation.AnnotationPosition.y, annotation.AnnotationPosition.z), annotation.AnnotationRotation, newText, newButton, annotation.AnnotationText, annotation.annotationCount, annotation.heading);




                }
            }

        }
        SettingAllignmentOFModel(model.transform.GetChild(0).gameObject);

    }

    public void SettingAllignmentOFModel(GameObject downloadedModelParent)
    {
    }

    public void SetBoxColliderBasedOnSize(GameObject model)
    {
        BoxCollider boxCollider;
        // Get the BoxCollider component attached to the GameObject
        boxCollider = model.GetComponent<BoxCollider>();

        // Check if the BoxCollider component exists
        if (boxCollider != null)
        {
            // Get all child Renderers
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            // Filter Renderers that have a MeshRenderer component
            List<Renderer> meshRenderers = new List<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer is MeshRenderer && renderer.tag != "notAChild" && renderer.name != "Quad")
                {
                    meshRenderers.Add(renderer);
                }
            }

            // Check if there are any child Renderers with MeshRenderer component
            if (meshRenderers.Count > 0)
            {
                // Calculate the combined bounds of all the filtered renderers
                Bounds bounds = meshRenderers[0].bounds;
                foreach (Renderer renderer in meshRenderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                // Calculate the center position relative to the GameObject's position
                Vector3 centerOffset = bounds.center - model.transform.position;

                // Set the BoxCollider's center to the calculated center offset
                boxCollider.center = centerOffset;

                // Set the BoxCollider's size based on the combined bounds
                boxCollider.size = bounds.size;
            }
            else
            {
                // Warn if the GameObject and its children with MeshRenderer components are not found
                DebugUtil.LogWarning("GameObject and its children do not have a Renderer component with MeshRenderer.");
            }
        }
        else
        {
            // Warn if the GameObject doesn't have a BoxCollider
            DebugUtil.LogWarning("GameObject does not have a BoxCollider component.");
        }
    }




    void OnButtonClick(string annotationText, string heading)
    {
        displayText.text = annotationText;
        description.text = heading;
    }
    public GameObject Detach(GameObject positionAlignment, GameObject downloadedModel)
    {
        return positionAlignment;
    }
    private void CreateEmptyObjectAtPosition(Vector3 annotationPosition, Quaternion rotation, TextMeshProUGUI newText, Button newButton, string annotationText, string buttonNumber, string heading)
    {

        BoxCollider boxCollider = GrabObj.GetComponent<BoxCollider>();
        float centerWidth = boxCollider.center.y + transform.position.y;


        GameObject emptyObject = new GameObject("EmptyObject");
        emptyObject.tag = "notAChild";
        emptyObject.transform.SetParent(model.transform.GetChild(0));
        emptyObject.transform.localPosition = annotationPosition;
        GameObject sourse = new GameObject("Sourse");
        Transform child = model.transform.GetChild(0);
        sourse.transform.SetParent(child.transform.GetChild(0));


        newText.transform.rotation = emptyObject.transform.rotation;
        GameObject newcubeButton = Instantiate(cubeButton, model.transform.GetChild(0));
        newcubeButton.tag = "notAChild";
        newcubeButton.transform.localScale = new Vector3(.05f, .05f, .05f);
        newcubeButton.transform.localRotation = emptyObject.transform.rotation;
        newcubeButton.transform.localPosition = emptyObject.transform.localPosition;
        sourse.transform.position = emptyObject.transform.position;
        newcubeButton.AddComponent<LookAt>();

        Canvas canvas = newcubeButton.GetComponentInChildren<Canvas>();
        canvas.worldCamera = Camera.main;
        Button[] buttons = canvas.GetComponentsInChildren<Button>();
        Transform canvasChild = cubeButton.transform.GetChild(0);
        InteractableUnityEventWrapper[] interactableWrappers = canvas.GetComponentsInChildren<InteractableUnityEventWrapper>();

        for (int i = 0; i < buttons.Length && i < interactableWrappers.Length; i++)
        {
            Button button = buttons[i];
            InteractableUnityEventWrapper interactableWrapper = interactableWrappers[i];

            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = buttonNumber;
            }

            button.onClick.AddListener(() => OnButtonClick(annotationText, heading));
            interactableWrapper.WhenSelect.AddListener(() => OnSelect(annotationText, heading));
        }
        tempCube.Add(newcubeButton);
        SettingCubeDistance(newcubeButton);
        Transform line = newcubeButton.transform.Find("LineRender");
        if (axis == Axis.horizontal)
        {
            sourcePoint = newcubeButton.transform.Find("sourcePointHorizontal");
        }
        else if (axis == Axis.horizontalRight)
        {
            sourcePoint = newcubeButton.transform.Find("sourcePointHorizontalRight");
        }
        else
        {
            sourcePoint = newcubeButton.transform.Find("sourcePointVertical");
        }
        if (line != null)
        {
            GameObject linePrefab = line.gameObject;
            Ir_Testing lineScript = linePrefab.GetComponent<Ir_Testing>();
            lineScript.SetUpPoints(sourcePoint.gameObject, emptyObject);


        }

    }
    public bool EvaluatePositionAxes(bool isaxes)
    {
        return isaxes;
    }
    private Transform sourcePoint;
    static int callCount = 0;
    static int alternateCallCount = 0;
    private bool isHorizontal;
    private enum Axis
    {
        horizontal,
        vertical,
        horizontalRight
    }
    private Axis axis;
    public void SettingCubeDistance(GameObject newcubeButton)
    {
        float offset = 0.2511871f;
        float buffer = 0.01f;
        Vector3 basePosition = newcubeButton.transform.localPosition;

        // Prioritized direction candidates: right, left, up
        List<Vector3> candidateOffsets = new List<Vector3>();

        if (callCount < 1)
        {
            if (basePosition.x < 0)
            {
                candidateOffsets.Add(new Vector3(-offset, 0, 0)); // left
                axis = Axis.horizontal;
                callCount++;
                isHorizontal = true;
            }
            else
            {
                if (alternateCallCount < 1)
                {
                    candidateOffsets.Add(new Vector3(offset, 0, 0)); // right
                    axis = Axis.horizontalRight;
                    alternateCallCount++;
                    isHorizontal = true;
                }
                else
                {
                    candidateOffsets.Add(new Vector3(0, offset, 0)); // up
                    axis = Axis.vertical;
                    alternateCallCount = 0;
                    isHorizontal = false;
                }
            }
        }
        else
        {
            candidateOffsets.Add(new Vector3(0, offset, 0)); // up
            axis = Axis.vertical;
            isHorizontal = false;
            callCount = 0;
        }

        // Extra fallback positions (never down)
        candidateOffsets.Add(new Vector3(offset, offset, 0));
        candidateOffsets.Add(new Vector3(-offset, offset, 0));
        candidateOffsets.Add(new Vector3(offset * 2, 0, 0));
        candidateOffsets.Add(new Vector3(-offset * 2, 0, 0));

        foreach (Vector3 offsetDir in candidateOffsets)
        {
            Vector3 testPosition = basePosition + offsetDir;
            bool isColliding = false;

            foreach (GameObject existing in tempCube)
            {
                if (existing == null || existing == newcubeButton) continue;
                float distance = Vector3.Distance(testPosition, existing.transform.localPosition);
                if (distance < offset - buffer)
                {
                    isColliding = true;
                    break;
                }
            }

            if (!isColliding)
            {
                newcubeButton.transform.localPosition = testPosition;

                if (offsetDir == new Vector3(offset, 0, 0))       // Right
                    newcubeButton.transform.eulerAngles = new Vector3(0, 90, 0);
                else if (offsetDir == new Vector3(-offset, 0, 0)) // Left
                    newcubeButton.transform.eulerAngles = new Vector3(0, 270, 0);
                else if (offsetDir == new Vector3(0, offset, 0))  // Up
                    newcubeButton.transform.eulerAngles = new Vector3(0, 0, 0);
                else                                              // Diagonal/fallback
                    newcubeButton.transform.eulerAngles = new Vector3(0, 180, 0);


                DebugUtil.Log("CubeButton positioned at: " + testPosition + " with rotation: " + newcubeButton.transform.eulerAngles);
                return;
            }
        }

        // Fallback if no non-colliding spot is found
        Vector3 fallbackPosition = basePosition + new Vector3(0, offset * 2, 0); // Still upward
        newcubeButton.transform.localPosition = fallbackPosition;
        newcubeButton.transform.eulerAngles = new Vector3(0, 0, 0);
        DebugUtil.LogWarning("Used fallback position for cubeButton.");
    }

    void OnSelect(string annotationText, string heading)
    {
        displayText.text = annotationText;
        description.text = heading;
    }

}

