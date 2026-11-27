using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Klever.VR
{
    public static class ClassroomBoardLayout
    {
        public struct Frame
        {
            public Vector3 Center, Right, Up, Forward;
            public float Width, Height, Depth;
            public Quaternion Rotation => Quaternion.LookRotation(Forward, Up);
            public Vector3 Front => Center - Forward * (Depth + 0.035f);
        }
        public static Renderer Surface(ChangeClassRoom room)
        {
            var obj = room.classRoom == 1 ? room.boardB : room.classRoom == 2 ? room.boardC : room.boardA;
            if (obj == null) return null;
            var own = obj.GetComponent<Renderer>();
            if (own != null) return own;
            foreach (var candidate in obj.GetComponentsInChildren<Renderer>(true)) if (candidate.name == "Board") return candidate;
            return null;
        }
        public static Frame Measure(Renderer surface, Vector3 viewer)
        {
            var b = surface.localBounds;
            var axes = new[] {surface.transform.TransformVector(Vector3.right),surface.transform.TransformVector(Vector3.up),surface.transform.TransformVector(Vector3.forward)};
            int thin = 0;
            for(int i=1;i<3;i++) if(b.size[i]*axes[i].magnitude < b.size[thin]*axes[thin].magnitude) thin=i;
            int vertical = (thin+1)%3;
            for(int i=0;i<3;i++) if(i!=thin && Mathf.Abs(Vector3.Dot(axes[i].normalized,Vector3.up)) > Mathf.Abs(Vector3.Dot(axes[vertical].normalized,Vector3.up))) vertical=i;
            int horizontal = 3-thin-vertical;
            var center=surface.transform.TransformPoint(b.center);
            var up=axes[vertical].normalized;
            if(Vector3.Dot(up,Vector3.up)<0) up=-up;
            var forward=Vector3.Cross(axes[horizontal],up).normalized;
            if(Vector3.Dot(forward,viewer-center)>0) forward=-forward;
            var right=Vector3.Cross(up,forward).normalized;
            up=Vector3.Cross(forward,right).normalized;
            return new Frame {Center=center,Right=right,Up=up,Forward=forward,
                Width=b.size[horizontal]*Mathf.Abs(Vector3.Dot(axes[horizontal],right)),
                Height=b.size[vertical]*Mathf.Abs(Vector3.Dot(axes[vertical],up)),
                Depth=b.extents[thin]*Mathf.Abs(Vector3.Dot(axes[thin],forward))};
        }
#if KLEVER_DPN
        // DPVR's board projection must never reposition the authored Quest WebView.
        public static void FitSimulation(RectTransform viewport, Renderer surface, Vector3 viewer, RectTransform next = null, RectTransform clock = null)
        {
            var frame = Measure(surface, viewer);
            if (frame.Width < 0.2f || frame.Height < 0.2f) return;
            // Keep the simulation screen large and place compact clock / Next pills cleanly below it.
            var face = frame.Center - frame.Forward * (frame.Depth + 0.008f);
            FitBoardRect(viewport, frame.Rotation, face + frame.Up * (frame.Height * 0.045f),
                frame.Width * 0.96f, frame.Height * 0.86f);
            var footerCenter = face - frame.Up * (frame.Height * 0.442f);
            if (clock != null)
                FitBoardRect(clock, frame.Rotation, footerCenter - frame.Right * (frame.Width * 0.35f),
                    frame.Width * 0.24f, frame.Height * 0.065f);
            if (next != null)
                FitBoardRect(next, frame.Rotation, footerCenter + frame.Right * (frame.Width * 0.39f),
                    frame.Width * 0.16f, frame.Height * 0.065f);
        }

        private static void FitBoardRect(RectTransform rect, Quaternion rotation, Vector3 center, float maxWidth, float maxHeight)
        {
            var bounds = rect.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            rect.rotation = rotation;
            float width = rect.TransformVector(Vector3.right * bounds.width).magnitude;
            float height = rect.TransformVector(Vector3.up * bounds.height).magnitude;
            if (width <= 0.00001f || height <= 0.00001f) return;
            float factor = Mathf.Min(maxWidth / width, maxHeight / height);
            if (Mathf.Abs(factor - 1f) > 0.00001f) rect.localScale *= factor;
            rect.position += center - rect.TransformPoint(bounds.center);
        }

#endif
        public static void Fit(RectTransform rect, Renderer surface, Vector3 viewer)
        {
            var frame=Measure(surface,viewer);
            if(frame.Width<0.2f || frame.Height<0.2f || rect.rect.width<=0 || rect.rect.height<=0) return;
            // Keep reading level with the horizon, including the rolled space board.
            var forward=Vector3.ProjectOnPlane(frame.Forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<0.1f) forward=frame.Forward;
            rect.rotation=Quaternion.LookRotation(forward,Vector3.up);
            var content=RectTransformUtility.CalculateRelativeRectTransformBounds(rect);
            var horizontal=rect.TransformVector(Vector3.right*content.size.x);
            var vertical=rect.TransformVector(Vector3.up*content.size.y);
            float projectedWidth=Mathf.Abs(Vector3.Dot(horizontal,frame.Right))+Mathf.Abs(Vector3.Dot(vertical,frame.Right));
            float projectedHeight=Mathf.Abs(Vector3.Dot(horizontal,frame.Up))+Mathf.Abs(Vector3.Dot(vertical,frame.Up));
            if(projectedWidth<0.00001f || projectedHeight<0.00001f)return;
            float factor=Mathf.Min(frame.Width*0.76f/projectedWidth,frame.Height*0.82f/projectedHeight);
            rect.localScale*=factor;
            float halfWidth=projectedWidth*factor*0.5f, halfHeight=projectedHeight*factor*0.5f;
            float depth=(Mathf.Abs(Vector3.Dot(horizontal,frame.Forward))+Mathf.Abs(Vector3.Dot(vertical,frame.Forward)))*factor*0.5f;
            var desired=frame.Center+frame.Right*(-frame.Width*0.5f+0.18f+halfWidth)+frame.Up*(frame.Height*0.5f-0.16f-halfHeight);
            float distance=Vector3.Dot(frame.Center-viewer,frame.Forward);
            float perspective=Mathf.Clamp((distance-frame.Depth-0.045f)/(distance+depth),0.25f,1f);
            rect.localScale*=perspective;
            var center=viewer+(desired-viewer)*perspective;
            rect.position+=center-rect.TransformPoint(content.center);
        }
    }
    public sealed class BoardInstructionLayout : MonoBehaviour
    {
        public ChangeClassRoom Room;
        public void Refresh()
        {
            if(Room==null) return;
            var surface=ClassroomBoardLayout.Surface(Room);
            if(surface!=null) ClassroomBoardLayout.Fit((RectTransform)transform,surface,Camera.main!=null?Camera.main.transform.position:new Vector3(0,1.6f,0));
        }
        private void LateUpdate()=>Refresh();
    }
    public sealed class ClassroomBranding : MonoBehaviour
    {
        private ChangeClassRoom room;
        private Canvas badge;
        public Canvas Badge => badge;
        public static ClassroomBranding Configure(ChangeClassRoom room)
        {
            var brand=room.GetComponent<ClassroomBranding>();
            if(brand==null) brand=room.gameObject.AddComponent<ClassroomBranding>();
            brand.room=room;
            if(brand.badge==null) brand.Build();
#if !KLEVER_DPN
            foreach(var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                if(canvas.gameObject.scene==room.gameObject.scene && (canvas.name=="Controller Panel" || canvas.name=="Controller Panel(Clone)")) {
                    var guide=canvas.GetComponent<QuestControllerGuide>();
                    if(guide==null){guide=canvas.gameObject.AddComponent<QuestControllerGuide>();guide.Build();}
                    var layout=canvas.GetComponent<BoardInstructionLayout>();
                    if(layout==null) layout=canvas.gameObject.AddComponent<BoardInstructionLayout>();
                    foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
                    foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true)) { text.enableAutoSizing=true; text.fontSizeMax=text.fontSize; text.fontSizeMin=text.fontSize*0.65f; }
                    layout.Room=room;
                    layout.Refresh();
                }
#endif
            brand.Refresh();
            return brand;
        }
        private void Build()
        {
            var go=new GameObject("Klever classroom signature",typeof(RectTransform),typeof(Canvas),typeof(Image));
            go.layer=5; go.transform.SetParent(transform,false);
            badge=go.GetComponent<Canvas>(); badge.renderMode=RenderMode.WorldSpace;
            var rect=(RectTransform)go.transform; rect.sizeDelta=new Vector2(400,90); rect.localScale=Vector3.one*0.001f;
            var bg=go.GetComponent<Image>(); bg.color=new Color(0.02f,0.045f,0.055f,0.9f); bg.raycastTarget=false;
            var mark=new GameObject("Klever mark",typeof(RectTransform),typeof(RawImage)); mark.layer=5; mark.transform.SetParent(go.transform,false);
            var image=mark.GetComponent<RawImage>(); image.texture=Resources.Load<Texture2D>("KleverClassroomMark"); image.uvRect=new Rect(0.22f,0.35f,0.56f,0.30f); image.raycastTarget=false;
            var mr=(RectTransform)mark.transform; mr.sizeDelta=new Vector2(120,64); mr.anchoredPosition=new Vector2(-110,0);
            var word=new GameObject("Klever wordmark",typeof(RectTransform),typeof(TextMeshProUGUI)); word.layer=5; word.transform.SetParent(go.transform,false);
            var text=word.GetComponent<TextMeshProUGUI>(); text.font=TMP_Settings.defaultFontAsset; text.text="Klever"; text.fontSize=46; text.color=Color.white; text.alignment=TextAlignmentOptions.MidlineLeft; text.raycastTarget=false;
            text.rectTransform.sizeDelta=new Vector2(210,80); text.rectTransform.anchoredPosition=new Vector2(65,0);
        }
        public static bool ShouldShow(ChangeClassRoom room,Renderer surface,string mode)
        {
            if(room==null || surface==null || !surface.enabled || !surface.gameObject.activeInHierarchy)return false;
            if(room.classRooms==null || room.classRoom<0 || room.classRoom>=room.classRooms.Length || room.classRooms[room.classRoom]==null || !room.classRooms[room.classRoom].activeInHierarchy)return false;
            // Immersive media can replace the room with a skybox without hiding
            // every board object. Never draw branding over that content.
            if(mode=="video" || mode=="image360" || mode=="simulation")return false;
            if(room.addingImage!=null && room.addingImage.VideoSphere!=null && room.addingImage.VideoSphere.activeInHierarchy)return false;
            return true;
        }
        public void Refresh()
        {
            if(room==null || badge==null)return;
            var surface=ClassroomBoardLayout.Surface(room);
            string mode=Klass.Net.LiveViewPublisher.Instance!=null?Klass.Net.LiveViewPublisher.Instance.CurrentContentMode:string.Empty;
            bool visible=ShouldShow(room,surface,mode);
            badge.gameObject.SetActive(visible);
            if(!visible)return;
            var frame=ClassroomBoardLayout.Measure(surface,Camera.main!=null?Camera.main.transform.position:new Vector3(0,1.6f,0));
            var forward=Vector3.ProjectOnPlane(frame.Forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<0.1f)forward=frame.Forward;
            // Keep the signature below the board and level with the horizon.
            var viewer=Camera.main!=null?Camera.main.transform.position:new Vector3(0,1.6f,0);
            var rotation=Quaternion.LookRotation(forward,Vector3.up);
            float support=Mathf.Abs(Vector3.Dot(rotation*Vector3.right*0.2f,frame.Forward))+Mathf.Abs(Vector3.Dot(Vector3.up*0.045f,frame.Forward));
            float distance=Vector3.Dot(frame.Center-viewer,frame.Forward);
            float perspective=Mathf.Clamp((distance-frame.Depth-0.045f)/(distance+support),0.25f,1f);
            var halfWidth = rotation * Vector3.right * 0.2f;
            var halfHeight = Vector3.up * 0.045f;
            float badgeExtentUp = Mathf.Abs(Vector3.Dot(halfWidth, frame.Up)) + Mathf.Abs(Vector3.Dot(halfHeight, frame.Up));
            float badgeExtentRight = Mathf.Abs(Vector3.Dot(halfWidth, frame.Right)) + Mathf.Abs(Vector3.Dot(halfHeight, frame.Right));
            var desired = frame.Center + frame.Right * (frame.Width * 0.5f - badgeExtentRight - 0.10f)
                - frame.Up * (frame.Height * 0.5f + badgeExtentUp + 0.07f);
            badge.transform.localScale=Vector3.one*(0.001f*perspective);
            badge.transform.SetPositionAndRotation(viewer+(desired-viewer)*perspective,rotation);
            badge.worldCamera=Camera.main;
        }        private void LateUpdate()=>Refresh();
    }
}
