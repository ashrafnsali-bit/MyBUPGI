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
            debugText.text = ""; // Disabled debug text for cleaner view
        }
    }
}
