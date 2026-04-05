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
        container = GetComponent<RectTransform>();
        handle = transform.Find("Handle").GetComponent<RectTransform>();
        joystickRange = container.sizeDelta.x / 2f;
        InputVector = Vector2.zero;
        
        // Ensure the image components are correctly setup
        Image img = GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
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
