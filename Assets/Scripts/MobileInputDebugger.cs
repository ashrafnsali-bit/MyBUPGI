using UnityEngine;
using UnityEngine.UI;

public class MobileInputDebugger : MonoBehaviour
{
    public Text debugText;
    private Rigidbody playerRb;
    private PlayerMovementScript pms;

    void Start()
    {
        pms = Object.FindAnyObjectByType<PlayerMovementScript>();
        if (pms != null) playerRb = pms.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (debugText != null)
        {
            string groundedStatus = (pms != null && pms.grounded) ? "YES" : "NO";
            string vel = (playerRb != null) ? playerRb.linearVelocity.ToString() : "N/A";
            
            debugText.text = $"[DEBUG INFO]\n" +
                             $"Joystick Input: {MobileJoystick.InputVector}\n" +
                             $"Is Moving: {(MobileJoystick.InputVector.magnitude > 0.01f)}\n" +
                             $"Grounded: {groundedStatus}\n" +
                             $"Velocity: {vel}\n" +
                             $"Touch Count: {Input.touchCount}";
        }
    }
}
