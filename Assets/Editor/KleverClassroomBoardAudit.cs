using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Klever.VR.Editor
{
 public static class ClassroomBoardAudit
 {
  public static void Run()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/KlassMeta.unity",OpenSceneMode.Single);
   var room=UnityEngine.Object.FindObjectsOfType<ChangeClassRoom>(true).Single();
   foreach(var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) camera.gameObject.SetActive(false);
   var eye=new GameObject("Board audit eye").AddComponent<Camera>(); eye.tag="MainCamera"; eye.transform.position=new Vector3(0,1.6f,0); eye.fieldOfView=68; eye.aspect=16f/9;
   foreach(var existing in UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c=>c.name=="Controller Panel")) existing.gameObject.SetActive(false);
   var panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Controller Instruction Prefab/Controller Panel.prefab"));
#if KLEVER_DPN
   var dpvr=panel.AddComponent<Klever.DPVR.DpvrControllerGuide>(); dpvr.Build();
#endif
   panel.name="Controller Panel";
   ClassroomBranding.Configure(room);
   var corners=new Vector3[4];
   for(int index=0;index<3;index++)
   {
    var selected=new[]{room.boardA,room.boardB,room.boardC}[index];
    room.AssignClassRoom("classEnvironment "+(index+1));
    var originalRotation=selected.transform.rotation;
    if(new[] {room.boardA,room.boardB,room.boardC}.Count(b=>b.activeSelf)!=1) throw new Exception("Previous classroom board is still active.");
    var surface=ClassroomBoardLayout.Surface(room); if(surface==null) throw new Exception("Board missing in classroom "+index);
    ClassroomBoardLayout.Fit((RectTransform)panel.transform,surface,eye.transform.position);
    var scale=panel.transform.localScale;
    for(int repeat=0;repeat<20;repeat++) ClassroomBoardLayout.Fit((RectTransform)panel.transform,surface,eye.transform.position);
    if(Vector3.Distance(scale,panel.transform.localScale)>0.000001f) throw new Exception("Guide scale drifts while fitting.");
    if(Vector3.Dot(panel.transform.up,Vector3.up)<0.999f)throw new Exception("Guide inherits board roll.");
    if(Quaternion.Angle(selected.transform.rotation,originalRotation)>0.001f)throw new Exception("Guide fitting changed the classroom board geometry.");
    var frame=ClassroomBoardLayout.Measure(surface,eye.transform.position);

    foreach(var rect in panel.GetComponentsInChildren<RectTransform>()) {
    rect.GetWorldCorners(corners);
    foreach(var corner in corners){var direction=corner-eye.transform.position; float t=Vector3.Dot(frame.Center-eye.transform.position,frame.Forward)/Vector3.Dot(direction,frame.Forward); var projected=eye.transform.position+direction*t; var offset=projected-frame.Center;
     if(Mathf.Abs(Vector3.Dot(offset,frame.Right))>frame.Width*0.5f-0.02f || Mathf.Abs(Vector3.Dot(offset,frame.Up))>frame.Height*0.5f-0.02f)throw new Exception("Instruction canvas escapes classroom board "+index);
    }
    }
    Canvas.ForceUpdateCanvases();
    foreach(var text in panel.GetComponentsInChildren<TMP_Text>()) {text.ForceMeshUpdate(); if(text.isTextOverflowing) throw new Exception("Instruction text overflows: "+text.name);}
    var web = room.webManager;
    var viewport = (RectTransform)web._webViewPrefab.transform;
    var next = (RectTransform)web.buttonNext.transform;
    var clock = (RectTransform)web.timer.transform.parent;
    var controls = new[] { viewport, next, clock };
    var positions = controls.Select(control => control.localPosition).ToArray();
    var rotations = controls.Select(control => control.localRotation).ToArray();
    var scales = controls.Select(control => control.localScale).ToArray();
    var sizes = controls.Select(control => control.sizeDelta).ToArray();
    var components = web.webView.GetComponentsInChildren<Component>(true).Length;
    for (int repeat = 0; repeat < 20; repeat++) web.PrepareEmbeddedView();
    if (Vector3.Distance(web.simulation.transform.position, web.temp.transform.position) > 0.000001f ||
        Quaternion.Angle(web.simulation.transform.rotation, web.temp.transform.rotation) > 0.001f)
     throw new Exception("Quest WebView missed its authored classroom anchor.");
    if (web.webView.GetComponentsInChildren<Component>(true).Length != components)
     throw new Exception("Quest WebView added replacement pointer controls.");
    for (int controlIndex = 0; controlIndex < controls.Length; controlIndex++)
    {
     var control = controls[controlIndex];
     if (Vector3.Distance(control.localPosition, positions[controlIndex]) > 0.000001f ||
         Quaternion.Angle(control.localRotation, rotations[controlIndex]) > 0.001f ||
         Vector3.Distance(control.localScale, scales[controlIndex]) > 0.000001f ||
         Vector2.Distance(control.sizeDelta, sizes[controlIndex]) > 0.000001f)
      throw new Exception("Quest WebView changed authored geometry in room " + index + ": " + control.name);
    }
    if (Quaternion.Angle(selected.transform.rotation, originalRotation) > 0.001f)
     throw new Exception("Quest simulation changed the classroom board geometry.");
    Debug.Log("[QUEST-WEBVIEW-AUDIT] room=" + (index + 1) + " authored floating canvas, controls and anchor preserved PASS");
    var brand=ClassroomBranding.Configure(room); brand.Refresh();
    if(brand.Badge.GetComponentsInChildren<Graphic>(true).Any(g=>g.raycastTarget) || brand.Badge.GetComponentsInChildren<Collider>(true).Length>0) throw new Exception("Branding blocks interactions.");
    if(!brand.Badge.gameObject.activeSelf)throw new Exception("Logo hidden in visible classroom.");
    ((RectTransform)brand.Badge.transform).GetWorldCorners(corners);
    foreach(var corner in corners){var direction=corner-eye.transform.position; float t=Vector3.Dot(frame.Center-eye.transform.position,frame.Forward)/Vector3.Dot(direction,frame.Forward); var projected=eye.transform.position+direction*t; var offset=projected-frame.Center;if(Vector3.Dot(offset,frame.Up)>-frame.Height*0.5f-0.02f)throw new Exception("Logo overlaps the board.");}
    foreach(var mode in new[]{"video","image360","simulation"})if(ClassroomBranding.ShouldShow(room,surface,mode))throw new Exception("Logo leaks into immersive content.");
    room.classRooms[index].SetActive(false);brand.Refresh();if(brand.Badge.gameObject.activeSelf)throw new Exception("Logo remains when classroom is hidden.");
    room.classRooms[index].SetActive(true);surface.gameObject.SetActive(false);brand.Refresh();if(brand.Badge.gameObject.activeSelf)throw new Exception("Logo remains when board is hidden.");
    surface.gameObject.SetActive(true);brand.Refresh();
    if(Resources.Load<Texture2D>("KleverClassroomMark")==null) throw new Exception("Classroom logo asset missing.");
    eye.transform.LookAt(frame.Center,Vector3.up);
    var prefix=Environment.GetEnvironmentVariable("KLEVER_BOARD_PREVIEW");
    if(!string.IsNullOrEmpty(prefix)) Render(eye,prefix+"_ROOM"+(index+1)+".png");
    Debug.Log("[BOARD-LAYOUT-AUDIT] room="+(index+1)+" contained, text fits, scale stable, branding non-interactive PASS");
   }
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  }
  private static void Render(Camera eye,string path)
  {
   var target=new RenderTexture(1600,900,24); eye.targetTexture=target; eye.Render(); RenderTexture.active=target;
   var texture=new Texture2D(1600,900,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
   RenderTexture.active=null; eye.targetTexture=null; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
  }
 }
}
