using UnityEngine;

public class MobileOrientationManager : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceLandscapeOrientation()
    {
        // Disable vertical portrait orientations
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

        // Enable horizontal landscape orientations
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // Immediately force the screen to horizontal landscape
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    private void Awake()
    {
        ApplyLandscapeOrientation();
    }

    private void Start()
    {
        ApplyLandscapeOrientation();
    }

    public static void ApplyLandscapeOrientation()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
