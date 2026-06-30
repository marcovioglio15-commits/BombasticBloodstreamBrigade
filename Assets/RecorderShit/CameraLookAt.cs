using UnityEngine;

public class CameraLookAt : MonoBehaviour
{

    Transform cameraTarget;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var camLookAt = GameObject.FindWithTag("Player");
        if (camLookAt)
            cameraTarget = camLookAt.transform;
        else
            Debug.LogError("Camera: Player Not Found");
    }

    private void Update()
    {
        var camLookAt = GameObject.FindWithTag("Player");
        if (camLookAt)
            cameraTarget = camLookAt.transform;
        else
            Debug.LogError("Camera: Player Not Found");

        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (cameraTarget)
            transform.LookAt(cameraTarget);
    }
}
