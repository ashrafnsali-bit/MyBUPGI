using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake instance;
    private Vector3 originalPos;
    private float shakeDuration = 0f;
    private float shakeMagnitude = 0.1f;
    private float dampingSpeed = 1.0f;
    private Transform camTransform;

    void Awake()
    {
        instance = this;
        camTransform = transform;
    }

    void OnEnable()
    {
        originalPos = camTransform.localPosition;
    }

    void Update()
    {
        if (shakeDuration > 0)
        {
            camTransform.localPosition = originalPos + Random.insideUnitSphere * shakeMagnitude;
            shakeDuration -= Time.deltaTime * dampingSpeed;
        }
        else
        {
            shakeDuration = 0f;
            camTransform.localPosition = originalPos;
        }
    }

    public void TriggerShake(float duration = 0.1f, float magnitude = 0.1f)
    {
        originalPos = camTransform.localPosition;
        shakeDuration = duration;
        shakeMagnitude = magnitude;
    }
}
