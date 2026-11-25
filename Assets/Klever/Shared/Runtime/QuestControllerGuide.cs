using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Klever.VR
{
 public sealed class QuestControllerGuide : MonoBehaviour
 {
  public const string Instructions="<b>POINT & SELECT</b>\nAim the controller ray. Press Trigger to select a button.\n\n<b>HOLD A 3D MODEL</b>\nAim at the model and hold Grip to grab it.\nMove your hand slowly. Release Grip to leave the model.\n\n<b>ROTATE & RESIZE</b>\nWhile holding Grip, use the thumbstick left / right to rotate.\nUse the thumbstick up / down to resize.\n\n<b>ANNOTATIONS & SOUND</b>\nRelease the model. Press Trigger on a label or speaker icon.";
  public void Build()
  {
   var original=GetComponentInChildren<TMP_Text>(true);
   var font=original!=null?original.font:TMP_Settings.defaultFontAsset;
   Sprite sprite=null;
   foreach(var image in GetComponentsInChildren<Image>(true)) if(image.sprite!=null){sprite=image.sprite;break;}
   foreach(Transform child in transform) child.gameObject.SetActive(false);
   var content=new GameObject("Quest controller instructions",typeof(RectTransform),typeof(Image)); content.transform.SetParent(transform,false);
   var rect=(RectTransform)content.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
   var background=content.GetComponent<Image>(); background.color=new Color(0.035f,0.075f,0.12f,0.96f); background.raycastTarget=false;
   Text(content.transform,font,"Meta Quest",new Vector2(80,-55),new Vector2(1750,110),76);
   Text(content.transform,font,Instructions,new Vector2(80,-200),new Vector2(1260,820),43);
   Text(content.transform,font,"QUEST CONTROLLERS",new Vector2(1400,-210),new Vector2(450,90),36);
   var diagram=new GameObject("Quest controller diagram",typeof(RectTransform),typeof(Image)); diagram.transform.SetParent(content.transform,false);
   var imageControl=diagram.GetComponent<Image>(); imageControl.sprite=sprite; imageControl.preserveAspect=true; imageControl.raycastTarget=false;
   var imageRect=(RectTransform)diagram.transform; imageRect.anchorMin=imageRect.anchorMax=imageRect.pivot=new Vector2(0,1); imageRect.anchoredPosition=new Vector2(1390,-320); imageRect.sizeDelta=new Vector2(450,340);
   Text(content.transform,font,"<b>GRIP</b>\nHold to grab\n\n<b>TRIGGER</b>\nSelect buttons / labels",new Vector2(1400,-710),new Vector2(450,300),36);
  }
  private static void Text(Transform parent,TMP_FontAsset font,string value,Vector2 position,Vector2 size,float fontSize)
  {
   var obj=new GameObject("Quest guide text",typeof(RectTransform),typeof(TextMeshProUGUI)); obj.transform.SetParent(parent,false);
   var text=obj.GetComponent<TextMeshProUGUI>(); text.font=font; text.text=value; text.fontSize=fontSize; text.enableAutoSizing=true; text.fontSizeMin=fontSize*0.8f; text.fontSizeMax=fontSize; text.enableWordWrapping=true; text.overflowMode=TextOverflowModes.Truncate; text.color=Color.white; text.raycastTarget=false;
   text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(0,1); text.rectTransform.anchoredPosition=position; text.rectTransform.sizeDelta=size;
  }
 }
}