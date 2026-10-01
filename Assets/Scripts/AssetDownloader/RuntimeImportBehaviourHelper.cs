using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus;
using SocketIOClient.Transport;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine.XR.Interaction.Toolkit;

public class RuntimeImportBehaviourHelper : MonoBehaviour
{
    // Start is called before the first frame update
    private GoogleTextToSpeech googleTextToSpeech;
    private GameObject model;
    private bool isDestroy;
    private GameObject extractModel;
    private RuntimeImportBehaviour runtimeImportBehaviour;
    private ModelScale modelScale;

    void Awake()
    {
       
         runtimeImportBehaviour =gameObject.GetComponent<RuntimeImportBehaviour>();
        modelScale=FindObjectOfType<ModelScale>();
    }
    public void FindingModel()
    {
        if (runtimeImportBehaviour == null)
            runtimeImportBehaviour = GetComponent<RuntimeImportBehaviour>();
        if (runtimeImportBehaviour == null)
        {
            Debug.LogError("[RuntimeImportBehaviourHelper] RuntimeImportBehaviour is missing; cannot set up the model.");
            return;
        }
        googleTextToSpeech = FindObjectOfType<GoogleTextToSpeech>();
        model = GameObject.FindWithTag(runtimeImportBehaviour.tagName);

        if (model == null)
        {
            DebugUtil.Log("[RuntimeImportBehaviourHelper] No model found to set up (download was skipped).");
            return;
        }

        GameObject emptyObject = new GameObject("Parent");
        emptyObject.transform.localPosition =gameObject.transform.position;
        emptyObject.transform.rotation = Quaternion.identity;
        emptyObject.transform.localScale = Vector3.one;
       
        model.transform.SetParent(emptyObject.transform);
        DebugUtil.Log(emptyObject.transform.GetChild(0).name);
        AddProperty(emptyObject);
        model.transform.position = Vector3.zero;
        emptyObject.tag = "3dmodel";
        emptyObject.transform.GetChild(0).transform.localPosition = Vector3.zero;
        string modelUrl = PlayerPrefs.GetString(StaticStrings.modelUrl);
        var details = new SimpleJSON.JSONObject();
        details["name"] = PlayerPrefs.GetString(StaticStrings.modelName);
        Klass.Net.LiveViewPublisher.Instance?.ShowContent("model3d", modelUrl, modelUrl, details);

    }
    public void AddProperty(GameObject parentObject)
    {

        if (parentObject.transform.childCount > 0)
        {
            parentObject.transform.GetChild(0).transform.localPosition = Vector3.zero;
            Transform firstChild = parentObject.transform;
            GameObject grabObj = GameObject.Find("GrabParent");
            extractModel = firstChild.gameObject;
            BoxCollider boxCollider = firstChild.GetComponent<BoxCollider>();
            //firstChild.transform.SetParent(grabObj.transform,false);
            if (boxCollider == null)
            {

            }
            else
            {
                DebugUtil.LogWarning("The child already has a BoxCollider.");
            }
        }
        else
            {
                DebugUtil.LogWarning("The parent object has no children.");
            }



        }

    public void AddinSmallBoxCollider(GameObject model)
    {
        if (model.GetComponent<BoxCollider>()!=null)
        {
            BoxCollider boxCollider = model.GetComponent<BoxCollider>();
            boxCollider.size = new Vector3(0.1f, 0.1f, 0.1f);
        }
        else
        {
            BoxCollider boxCollider = model.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(0.1f, 0.1f, 0.1f);
        }
        
    }
    public void AddingBoxColliderBasedOnSize(GameObject model)
    {
        // Add a BoxCollider component to the GameObject
        BoxCollider boxCollider = model.AddComponent<BoxCollider>();

        // Check if the GameObject has a Renderer component
        Renderer modelRenderer = model.GetComponent<Renderer>();
        if (modelRenderer != null)
        {
            // If the GameObject has a Renderer component,
            // adjust the BoxCollider size to match the Renderer's bounds
            boxCollider.size = modelRenderer.bounds.size;
            // Set the center of the BoxCollider to the center of the GameObject
            boxCollider.center = modelRenderer.bounds.center - model.transform.position;
        }
        else
        {
            // If the GameObject does not have a Renderer, check its children
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                // Calculate the combined bounds of all the children's renderers
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                // Calculate the center position relative to the GameObject's position
                Vector3 centerOffset = bounds.center - model.transform.position;

                // Set the BoxCollider's center to the calculated center offset
                boxCollider.center = centerOffset;

                // Set the BoxCollider's size based on the combined bounds
                boxCollider.size = new Vector3(0.1f, 0.1f, 0.1f);
            }
            else
            {
                DebugUtil.LogWarning("GameObject does not have a Renderer or Renderer in children to base the BoxCollider size on.");
            }
        }
    }

    public void AssetDownloadCallback()
    {
       FindingModel();
        Invoke("StartSpeaking", 2f);
    }
    public void customCallBack(string text)
    {
        FindingModel();
        if (googleTextToSpeech != null)
            googleTextToSpeech.StartTalking(text);
        else
            Debug.LogError("[RuntimeImportBehaviourHelper] GoogleTextToSpeech is missing; cannot start narration.");
    }
    public void CustomStartSpeak()
    {
        Invoke("StartSpeaking", 2f);
    }
    public void StartSpeaking()
    {
        if (googleTextToSpeech == null)
            googleTextToSpeech = FindObjectOfType<GoogleTextToSpeech>();
        if (googleTextToSpeech != null)
            googleTextToSpeech.AssistantStartTalking();
        else
            Debug.LogError("[RuntimeImportBehaviourHelper] GoogleTextToSpeech is missing; cannot start narration.");
    }
    public void DisableObject()
    {
        // Use this helper's wrapper; other imported models can share its name.
        GameObject modelRoot = extractModel;
        if (modelRoot == null)
            return;

        Transform parent = modelRoot.transform.parent;
        if (parent != null)
        {
            BoxCollider parentCollider = parent.GetComponent<BoxCollider>();
            if (parentCollider != null)
                parentCollider.enabled = false;
        }

        modelRoot.SetActive(false);
        Destroy(modelRoot);
        extractModel = null;
        model = null;
    }
    // Update is called once per frame
    void Update()
    {
        if (extractModel != null && extractModel.activeInHierarchy)
            Klass.Net.LiveViewPublisher.Instance?.PublishTransform(extractModel.transform);
        if(isDestroy)
        {

            isDestroy = false;
            DisableObject();

        }
        
    }
  
}
