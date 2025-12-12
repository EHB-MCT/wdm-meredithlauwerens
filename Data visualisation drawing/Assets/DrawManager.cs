using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using UnityEngine.UI;
using TMPro;



public class DrawManagerInput : MonoBehaviour
{
    [System.Serializable]
    public class StrokePayload
    {
        public string uid;
        public string color;
        public double duration;
        public List<Vector3Serializable> points;
    }

    [System.Serializable]
    public class Vector3Serializable
    {
        public float x;
        public float y;
        public float z;

        public Vector3Serializable(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }
    }
    public class DrawingPayload //all strokes in 1 package
    {
        public string uid;
        public double totalDuration;
        public List<StrokePayload> strokes;
        public int eraseCount;
        public int undoCount;
        public int redoCount;
        public int increaseWidthCount;
        public int decreaseWidthCount;
    }

    [System.Serializable]
    public class StrokeData
    {
        public Color color;
        public string colorName;
        public float duration;
        public List<Vector3> points;
        public LineRenderer lineRenderer;
        public int eraseCount = 0; //total eraser used
        public int undoCount = 0;  //total undo used
        public GameObject lineObject; //to detect stroke to erase
    }

   // Undo/Redo stacks
    private Stack<ActionRecord> undoStack = new();
    private Stack<ActionRecord> redoStack = new();
    public Button undoBtn;
    public Button redoBtn;

    private int totalEraseCount = 0;
    private int totalUndoCount = 0;

    private bool isErasing = false;

    private string userId;
    private string colorName = "Black";

    void Start()
    {
        userId = System.Guid.NewGuid().ToString();
        Debug.Log($"User ID: {userId}");
        strokeWidthText = GameObject.Find("StrokeWidthText").GetComponent<TMP_Text>();
        UpdateStrokeWidthText(); //call this to initialize the text
    }


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

    //cursor changes
    [Header("Cursor Icons")]
    public Texture2D penCursor;
    public Texture2D eraserCursor;

    private Vector2 cursorHotspot = Vector2.zero;

    public float minLineWidth = 0.01f; //min allowed stroke width
    public float maxLineWidth = 0.1f;  //max allowed stroke width
    public float widthStep = 0.01f;    //amount to increase/decrease by
    public TMP_Text strokeWidthText; //assign in the Unity Editor

    private int totalRedoCount = 0;
    private int totalIncreaseWidthCount = 0;
    private int totalDecreaseWidthCount = 0;

    

    //input callbacks (connecting via PlayerInput)
    public void OnDraw(InputAction.CallbackContext context)
    {
        if (isErasing) return; //ignore draw input while erasing
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
        Debug.Log("Right click detected");
    }

    void Update()
    {
        if (!isErasing && isDrawing)
            DrawStroke();
        if (isErasing)
            CheckEraseClick();

        //enable/disable undo/redo buttons
        if (undoBtn != null)
            undoBtn.interactable = strokes.Count > 0;

        if (redoBtn != null)
            redoBtn.interactable = redoStack.Count > 0;
    }



    void StartStroke()
    {
        if (isErasing) return; //not drawing when erasing mode
        isDrawing = true;

        //make new line
        currentLine = new GameObject("Line").AddComponent<LineRenderer>();

        //unique material per line
        currentLine.material = new Material(lineMaterial);

        //width
        currentLine.startWidth = lineWidth;
        currentLine.endWidth = lineWidth;

        //use gradient for color
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(drawColor, 0f), new GradientColorKey(drawColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        currentLine.colorGradient = grad;

        //points list reset
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
            StrokeData stroke = new StrokeData()
            {
                color = drawColor,
                colorName = colorName,
                duration = Time.time - strokeStartTime,
                points = new List<Vector3>(points),
                lineRenderer = currentLine
            };

            // create a parent object for that stroke
            stroke.lineObject = new GameObject("Stroke");
            currentLine.transform.parent = stroke.lineObject.transform;
            strokes.Add(stroke);

            //add colliders to each segment of that stroke
            AddCollidersToStroke(stroke);

            SendStrokeData(stroke);
            Debug.Log($"Stroke saved: {stroke.points.Count} points, duration {stroke.duration:F2}s");
        }
    }

    void AddCollidersToStroke(StrokeData stroke)
    {
        for (int i = 1; i < stroke.points.Count; i++)
        {
            Vector3 start = stroke.points[i - 1];
            Vector3 end = stroke.points[i];

            GameObject segmentObj = new GameObject("LineSegment");
            segmentObj.transform.parent = stroke.lineObject.transform;

            CapsuleCollider col = segmentObj.AddComponent<CapsuleCollider>();
            col.isTrigger = true;

            //set direction and length
            Vector3 dir = end - start;
            col.transform.position = start + dir / 2;
            col.transform.up = dir.normalized;
            col.height = dir.magnitude;
            col.radius = lineWidth / 2;
        }
    }

    //UI color choice
    public void SetColor(string newColorName)
    {
        colorName = newColorName; //save which color used

        switch (newColorName)
        {
            case "Red": drawColor = Color.red; break;
            case "Green": drawColor = Color.green; break;
            case "Blue": drawColor = Color.blue; break;
            case "Black": drawColor = Color.black; break;
            case "White": drawColor = Color.white; break;
            default: drawColor = Color.black; break;
        }

        Debug.Log($"Brush color set to: {drawColor} ({colorName})");

        //deselect UI button
        EventSystem.current.SetSelectedGameObject(null);

        Cursor.SetCursor(penCursor, cursorHotspot, CursorMode.Auto);
        isErasing = false;
        SetCursorToPen();
    }

    async void SendStrokeData(StrokeData stroke)
    {
        var serializedPoints = stroke.points.ConvertAll(p => new Vector3Serializable(p));

        StrokePayload payload = new StrokePayload
        {
            uid = userId,
            color = colorName,
            duration = stroke.duration,
            points = serializedPoints,
        };

        string json = JsonUtility.ToJson(payload);
        Debug.Log("Sending JSON: " + json);

        using (HttpClient client = new HttpClient())
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("http://localhost:5000/api/strokes", content);
                string responseBody = await response.Content.ReadAsStringAsync();
                Debug.Log($"Stroke data sent: {response.StatusCode}, Response: {responseBody}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error sending data: {e.Message}");
            }
        }
    }

    public void OnDone()
    {
        Debug.Log("Drawing finished. Sending full drawing...");
        SendFullDrawing();
    }

    async void SendFullDrawing()
    {
        //convert all strokes to StrokePayload
        List<StrokePayload> strokePayloads = new();

        foreach (var s in strokes)
        {
            var serializedPoints = new List<Vector3Serializable>();
            foreach (var p in s.points)
                serializedPoints.Add(new Vector3Serializable(p));

            strokePayloads.Add(new StrokePayload
            {
                uid = userId,
                color = s.colorName,
                duration = Math.Round(s.duration, 2),
                points = serializedPoints,
            });
        }

        //total time of drawing
        float totalDuration = 0f;
        foreach (var s in strokes)
            totalDuration += s.duration;

        //make master payload
        DrawingPayload payload = new DrawingPayload
        {
            uid = userId,
            totalDuration = Math.Round(totalDuration, 2),
            strokes = strokePayloads,
            eraseCount = totalEraseCount,
            undoCount = totalUndoCount,
            redoCount = totalRedoCount,
            increaseWidthCount = totalIncreaseWidthCount,
            decreaseWidthCount = totalDecreaseWidthCount
        };


        string json = JsonUtility.ToJson(payload);
        Debug.Log("Sending FULL drawing JSON:\n" + json);

        using (HttpClient client = new HttpClient())
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("http://localhost:5000/api/drawing", content);
                Debug.Log($"Full drawing sent: {response.StatusCode}");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error sending full drawing: " + e.Message);
            }
        }
    }

   public void SetEraserMode(bool eraseMode)
    {
        isErasing = eraseMode;
        Debug.Log(isErasing ? "Eraser enabled" : "Drawing mode enabled");

        if (isErasing)
        {
            SetCursorToEraser();
        }
        else
        {
            SetCursorToPen();
        }
    }


    void SetCursorToPen()
    {
        if (penCursor == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }
        Vector2 hotspot = new Vector2(penCursor.width / 2f, penCursor.height / 2f);
        Cursor.SetCursor(penCursor, hotspot, CursorMode.Auto);
    }

    void SetCursorToEraser()
    {
        if (eraserCursor == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }
        Vector2 hotspot = new Vector2(eraserCursor.width / 2f, eraserCursor.height / 2f);
        Cursor.SetCursor(eraserCursor, hotspot, CursorMode.Auto);
    }

    void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    void CheckEraseClick()
    {
        if (!Mouse.current.leftButton.isPressed) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            StrokeData strokeToErase = strokes.Find(s => s.lineObject != null && hit.collider != null && hit.collider.transform.IsChildOf(s.lineObject.transform));
            if (strokeToErase != null)
            {
                Destroy(strokeToErase.lineObject);
                strokes.Remove(strokeToErase);
                totalEraseCount++;
                Debug.Log($"Stroke erased, total strokes erased: {totalEraseCount}");
            }
        }
    }

     //action record for undo/redo
    private class ActionRecord
    {
        public StrokeData Stroke;
    }

    public void Undo()
    {
        if (strokes.Count == 0) return;

        StrokeData lastStroke = strokes[^1];
        undoStack.Push(new ActionRecord { Stroke = lastStroke });
        strokes.RemoveAt(strokes.Count - 1);
        lastStroke.lineObject.SetActive(false); //disable instead of destroy
        redoStack.Push(new ActionRecord { Stroke = lastStroke });
        totalUndoCount++;
        Debug.Log($"Undo performed, total undo count: {totalUndoCount}");
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;

        ActionRecord action = redoStack.Pop();
        strokes.Add(action.Stroke);
        action.Stroke.lineObject.SetActive(true);
        totalRedoCount++;
        Debug.Log("Redo performed");
    }

    public void OnPointerEnterButton()
    {
        ResetCursor(); //reset to default cursor
    }

    public void OnPointerExitButton()
    {
        if (isErasing)
            SetCursorToEraser();
        else
            SetCursorToPen();
    }

    public void IncreaseStrokeWidth()
    {
        lineWidth = Mathf.Min(lineWidth + widthStep, maxLineWidth);
        totalIncreaseWidthCount++;
        UpdateStrokeWidthText();
        Debug.Log("Stroke width increased to: " + lineWidth);
    }

    public void DecreaseStrokeWidth()
    {
        lineWidth = Mathf.Max(lineWidth - widthStep, minLineWidth);
        totalDecreaseWidthCount++;
        UpdateStrokeWidthText();
        Debug.Log("Stroke width decreased to: " + lineWidth);
    }



    void UpdateStrokeWidthText()
    {
        if (strokeWidthText != null)
            strokeWidthText.text = "Stroke Width: " + lineWidth.ToString("F2");
    }

}




