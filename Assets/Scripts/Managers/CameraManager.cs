using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera mainCam;
    public Camera predator1Cam;
    public Camera predator2Cam;
    public Camera squirrelCam;

    void Start()
    {
        SetActiveCamera(mainCam);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetActiveCamera(mainCam);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetActiveCamera(predator1Cam);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetActiveCamera(predator2Cam);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetActiveCamera(squirrelCam);
        }
    }

    void SetActiveCamera(Camera activeCam)
    {
        if (mainCam) mainCam.enabled = false;
        if (predator1Cam) predator1Cam.enabled = false;
        if (predator2Cam) predator2Cam.enabled = false;
        if (squirrelCam) squirrelCam.enabled = false;

        if (activeCam) activeCam.enabled = true;
    }
}