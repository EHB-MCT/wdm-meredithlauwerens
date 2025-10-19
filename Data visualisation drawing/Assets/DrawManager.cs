using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Belangrijk!
using UnityEngine.EventSystems;

public class DrawManagerInput : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Material lineMaterial;
    public Color drawColor = Color.black;
    public float lineWidth = 0.02f;

    private bool isDrawing = false;
    private Vector2 pointerPos;
    private LineRenderer currentLine;
    private List<Vector3> points = new();
    private List<StrokeData> strokes = new();
    private float strokeStartTime;

    // 🎨 Input callbacks (koppelen via PlayerInput)
    public void OnDraw(InputAction.CallbackContext context)
    {
        if (context.started)
            StartStroke();
        else if (context.performed)
            isDrawing = true;
        else if (context.canceled)
            EndStroke();
    }

    public void OnPosition(InputAction.CallbackContext context)
    {
        pointerPos = context.ReadValue<Vector2>();
    }

    public void OnRightClick(InputAction.CallbackContext context)
    {
        Debug.Log("Right click detected (optioneel)");
    }

    void Update()
    {
        if (isDrawing)
            DrawStroke();
    }

void StartStroke()
{
    isDrawing = true;

    // Maak nieuwe lijn
    currentLine = new GameObject("Line").AddComponent<LineRenderer>();

    // Uniek materiaal per lijn
    currentLine.material = new Material(lineMaterial);

    // Breedte
    currentLine.startWidth = lineWidth;
    currentLine.endWidth = lineWidth;

    // Gebruik gradient voor kleur
    Gradient grad = new Gradient();
    grad.SetKeys(
        new GradientColorKey[] { new GradientColorKey(drawColor, 0f), new GradientColorKey(drawColor, 1f) },
        new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    );
    currentLine.colorGradient = grad;

    // Puntenlijst reset
    currentLine.positionCount = 0;
    points.Clear();
    strokeStartTime = Time.time;
}




    void DrawStroke()
    {
        Ray ray = cam.ScreenPointToRay(pointerPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 hitPoint = hit.point;
            if (points.Count == 0 || Vector3.Distance(points[^1], hitPoint) > 0.01f)
            {
                points.Add(hitPoint);
                currentLine.positionCount = points.Count;
                currentLine.SetPosition(points.Count - 1, hitPoint);
            }
        }
    }

    void EndStroke()
    {
        isDrawing = false;
        if (points.Count > 1)
        {
            StrokeData stroke = new()
            {
                color = drawColor,
                duration = Time.time - strokeStartTime,
                points = new List<Vector3>(points)
            };
            strokes.Add(stroke);
            Debug.Log($"Stroke saved: {stroke.points.Count} points, duration {stroke.duration:F2}s");
        }
    }

    [System.Serializable]
    public class StrokeData
    {
        public Color color;
        public float duration;
        public List<Vector3> points;
    }

    // --- UI kleurkeuze ---
    public void SetColor(string colorName)
    {
        switch (colorName)
        {
            case "Red": drawColor = Color.red; break;
            case "Green": drawColor = Color.green; break;
            case "Blue": drawColor = Color.blue; break;
            case "Black": drawColor = Color.black; break;
            case "White": drawColor = Color.white; break;

            default: drawColor = Color.black; break;
        }

        Debug.Log($"Brush color set to: {drawColor}");

        // Verwijder selectie van de button
        EventSystem.current.SetSelectedGameObject(null);
    }

}




