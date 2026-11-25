using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RoomController : MonoBehaviour
{

    public virtual void ShowClassRomm(GameObject theClassTobeDisplay)
    {
        DebugUtil.Log("here we display room");
    }
}
public class ClassRoomOne : RoomController
{
    public override void ShowClassRomm(GameObject classOne)
    {
        classOne.SetActive(true);
    }

}
public class ClassRoomTwo : RoomController
{
    public override void ShowClassRomm(GameObject classOne)
    {
        classOne.SetActive(true);
    }

}
public class ClassRoomThree : RoomController
{
    public override void ShowClassRomm(GameObject classOne)
    {
        classOne.SetActive(true);
    }

}
public class ChangeClassRoom : MonoBehaviour
{
    public static ChangeClassRoom Instance;
    public int classRoom;
    void Awake()
    {
        Instance = this;
    }
    public GameObject[] classRooms;
    public Material[] skyBoxMaterials;
    //board UI Elements position according to the classroom
    public GameObject dummyBoard;
    public GameObject boardA, boardB, boardC;
    public Transform boardUI;
    public Transform boardUIPosA, boardUIPosB, boardUIPosC;

    //Simulation position according to the classroom
    public WebManager webManager;
    public GameObject classRoomASimulPos, classRoomBSimulPos, classRoomCSimulPos;

    //Gamification Position according to the classroom
    public Transform Gamification;
    public Transform classRoomAGamPos, classRoomBGamPos, classRoomCGamPos;
    public AddingImage addingImage;
    // Start is called before the first frame update
    void Start()
    {
        Klever.VR.ClassroomBranding.Configure(this);
       
    }
    public void AssignClassRoom(string roomDetails)
    {
        // The board screens live outside the classroom roots. Hide all old
        // screens before selecting the new room, avoiding overlapping boards.
        foreach (var board in new[] { boardA, boardB, boardC }) if (board != null) board.SetActive(false);
        RoomController classRoom1 = new ClassRoomOne();
        RoomController classRoom2 = new ClassRoomOne();
        RoomController classRoom3 = new ClassRoomOne();
        IEnumerator enumerator = classRooms.GetEnumerator();
        addingImage.VideoSphere.SetActive(false);
        while (enumerator.MoveNext())
        {
            GameObject currentRoom = (GameObject)enumerator.Current;
            currentRoom.SetActive(false);
        }
        switch (roomDetails)
        {
            case "classEnvironment 1":
                classRoom = 0;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                classRoom1.ShowClassRomm(classRooms[0]);
                ChangeSkyBox.SetSkybox(skyBoxMaterials[0]);
                boardA.SetActive(true);
                dummyBoard.SetActive(false);
                boardUI.position = boardUIPosA.position;
                boardUI.rotation = boardUIPosA.rotation;
                webManager.temp = classRoomASimulPos;
                Gamification.position = classRoomAGamPos.position;
                Gamification.rotation = classRoomAGamPos.rotation;
                break;
            case "classEnvironment 2":
                classRoom = 1;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                classRoom2.ShowClassRomm(classRooms[1]);
                ChangeSkyBox.SetSkybox(skyBoxMaterials[1]);
                boardB.SetActive(true);
                dummyBoard.SetActive(false);
                boardUI.position = boardUIPosB.position;
                boardUI.rotation = boardUIPosB.rotation;
                webManager.temp = classRoomBSimulPos;
                Gamification.position = classRoomBGamPos.position;
                Gamification.rotation = classRoomBGamPos.rotation;
                break;
            case "classEnvironment 3":
                classRoom = 2;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                float colorValue = 188f / 255f;
                RenderSettings.ambientLight = new Color(colorValue,colorValue,colorValue);
                classRoom3.ShowClassRomm(classRooms[2]);
                ChangeSkyBox.SetSkybox(skyBoxMaterials[2]);
                boardC.SetActive(true);
                dummyBoard.SetActive(false);
                boardUI.position = boardUIPosC.position;
                boardUI.rotation = boardUIPosC.rotation;
                webManager.temp = classRoomCSimulPos;
                Gamification.position = classRoomCGamPos.position;
                Gamification.rotation = classRoomCGamPos.rotation;
                break;

            default:
                print("Incorrect intelligence level.");
                break;
        }



    }

    public class ChangeSkyBox
    {
        public static Material[] skyBoxMaterialNew
        {
            get
            {
                if (ChangeClassRoom.Instance == null) return null;
                return ChangeClassRoom.Instance.skyBoxMaterials;
            }
        }
       
        public static void SetSkybox(Material newSkybox)
        {
            RenderSettings.skybox = newSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }
}
