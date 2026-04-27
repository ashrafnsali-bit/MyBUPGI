using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    public static Vector2 InputVector { get; set; }

    private RectTransform container;
    private RectTransform handle;
    private float joystickRange;

    void Awake()
    {
#if !UNITY_ANDROID && !UNITY_IOS
        gameObject.SetActive(false);
        return;
#endif
        container = GetComponent<RectTransform>();
        handle = transform.Find("Handle").GetComponent<RectTransform>();
        InputVector = Vector2.zero;

        // Ensure the image components are correctly setup
        Image img = GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
    }

    void Start()
    {
#if !UNITY_ANDROID && !UNITY_IOS
        return;
#endif
        // Force the canvas to update layout BEFORE reading the size,
        // so joystickRange is always correct for the current screen size.
        Canvas.ForceUpdateCanvases();
        joystickRange = container.rect.width / 2f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 pos;
        // Correctly handle overlay canvas by passing null for the camera
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, eventData.position, null, out pos))
        {
            // Calculate direction and magnitude within the range
            float x = pos.x / joystickRange;
            float y = pos.y / joystickRange;

            Vector2 rawInput = new Vector2(x, y);
            InputVector = (rawInput.magnitude > 1.0f) ? rawInput.normalized : rawInput;

            // Update handle position visually
            handle.anchoredPosition = new Vector2(InputVector.x * joystickRange * 0.8f, InputVector.y * joystickRange * 0.8f);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }

    void OnDisable()
    {
        InputVector = Vector2.zero;
    }
}
