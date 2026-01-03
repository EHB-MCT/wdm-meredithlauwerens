using UnityEngine;
using TMPro;
using UnityEngine.UI;

using UnityEngine.InputSystem;

using UnityEngine.EventSystems;

//for sending HTTP requests (sending drawing data to a server)
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

//standard C# utilities and collections
using System;
using System.Collections.Generic;
using System.Linq;

/*DATA CLASSES*/
public class DrawManagerInput : MonoBehaviour
{
    [System.Serializable]

    public class TopicDrawingData
    {
        public string topic;
        public bool usedReference;
        public DrawingPayload drawing;  //never null
    }

    public class SessionPayload
    {
        public string uid;
        public List<DrawManagerInput.TopicDrawingData> session;
    }

    [System.Serializable]
    public class StrokePayload
    {
        public string uid;
        public string color;
        public double duration;
        public BoundingBox bounds;
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

    [System.Serializable]
    //all strokes for 1 drawing
    public class DrawingPayload 
    {
        public string uid;
        public double totalDuration;
        public List<StrokePayload> strokes;
        public int colorChangeCount;

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

    [System.Serializable]
    //encapsulates the 3D area of a stroke
    public class BoundingBox
    {
        public float minX, maxX;
        public float minY, maxY;
        public float minZ, maxZ;

        public BoundingBox(Vector3 min, Vector3 max)
        {
            minX = min.x; maxX = max.x;
            minY = min.y; maxY = max.y;
            minZ = min.z; maxZ = max.z;
        }
    }

    [System.Serializable]
    public class StrokePayloadBoundingBox
    {
        public string uid;
        public string color;
        public double duration;
        public BoundingBox bounds;
    }

   //undo/redo stacks
   //ActionRecord stores strokes for undo/redo operations
    private Stack<ActionRecord> undoStack = new();
    private Stack<ActionRecord> redoStack = new();
    public Button undoBtn;
    public Button redoBtn;

    private int totalEraseCount = 0;
    private int totalUndoCount = 0;

    private bool isErasing = false;

    private string userId; //GUID generated for session
    private string colorName = "Black";
    private string lastColorName = "Black";
    private int colorChangeCount = 0;

    public List<string> topics = new List<string> { "Frog", "Cat", "Tree", "Car", "Dino" };
    private int currentTopicIndex = 0;
    private List<TopicDrawingData> sessionData = new List<TopicDrawingData>(); //sessionData stores all topics for sending later to backend

    private bool withReference = false; //user choice for current topic
    public Image referenceImageDisplay;  //where the reference image shows
    public List<Sprite> referenceSprites; //reference images for topics

    
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
    private bool canDraw = false;

    public ReferenceChoiceUI referenceChoiceUI;

    BoundingBox ComputeBounds(List<Vector3> points)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (Vector3 p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new BoundingBox(min, max);
    }


    void Start()
    {
        //generates a new UID for this session
        userId = System.Guid.NewGuid().ToString();
        Debug.Log($"New Session UID: {userId}");

        //sets up UI for stroke width display
        strokeWidthText = GameObject.Find("StrokeWidthText").GetComponent<TMP_Text>();
        UpdateStrokeWidthText();

        //starts experiment by prompting first topic reference choice
        PromptReferenceChoice();
    }

    /*DRAWING FLOW*/
    //shows UI for user to choose reference vs free draw
    public void PromptReferenceChoice()
    {
        referenceChoiceUI.ShowTopic(topics[currentTopicIndex]);
    }

    //sets withReference and shows/hides reference image
   public void ShowReference(bool useReference)
    {
        withReference = useReference;
        canDraw = true;

        if (useReference)
        {
            referenceImageDisplay.sprite = referenceSprites[currentTopicIndex];
            referenceImageDisplay.color = Color.white; 
            referenceImageDisplay.gameObject.SetActive(true);
            Debug.Log("Reference sprite: " + referenceSprites[currentTopicIndex].name);
        }
        else
        {
            referenceImageDisplay.gameObject.SetActive(false);
        }
        StartDrawingTopic();
    }

    //resets all counters and clears previous strokes for a new topic
    void StartDrawingTopic()
    {
        strokes.Clear(); //clear previous strokes
        colorChangeCount = 0;
        totalEraseCount = 0;
        totalUndoCount = 0;
        totalRedoCount = 0;
        totalIncreaseWidthCount = 0;
        totalDecreaseWidthCount = 0;

        Debug.Log("Topic started: " + topics[currentTopicIndex] + ", Reference: " + withReference);
        ResetTopicStats();
    }

    //handles input from mouse
    public void OnDraw(InputAction.CallbackContext context)
    {
        if (!canDraw) return;
        if (isErasing) return;

        if (context.started) //start new stroke
            StartStroke();
        else if (context.performed) //continue drawing
            isDrawing = true;
        else if (context.canceled) //finish stroke
            EndStroke();
    }

    //creates new LineRenderer and initializes stroke points
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

    //adds points to line as user moves
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

    //finalizes stroke, calculates bounding box and stores stroke data
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

            stroke.lineObject = new GameObject("Stroke");
            currentLine.transform.parent = stroke.lineObject.transform;
            strokes.Add(stroke);

            AddCollidersToStroke(stroke);

            // Send bounding box data instead of all points
            SendStrokeBoundingBox(stroke);

            Debug.Log($"Stroke saved: {stroke.points.Count} points, duration {stroke.duration:F2}s");
        }
    }

    /*STROKE MANAGEMENT*/
    //adds colliders to strokes for erasing by click
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

    //changes the drawing color and tracks color changes.
    public void SetColor(string newColorName)
    {
        //only count if color REALLY changed
        if (newColorName != lastColorName)
        {
            colorChangeCount++;
            lastColorName = newColorName;
        }

        colorName = newColorName;

        switch (newColorName)
        {
            case "Red": drawColor = Color.red; break;
            case "Green": drawColor = Color.green; break;
            case "Blue": drawColor = Color.blue; break;
            case "Black": drawColor = Color.black; break;
            case "White": drawColor = Color.white; break;
            default: drawColor = Color.black; break;
        }

        Debug.Log($"Color changed to {newColorName}. Total changes: {colorChangeCount}");

        EventSystem.current.SetSelectedGameObject(null);
        isErasing = false;
        SetCursorToPen();
    }

    //switch between drawing and erasing mode with cursor feedback
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

    /*UNDO/REDO*/
    //disables last stroke and pushes it to redo stack
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

    //re-enables last undone stroke
    public void Redo()
    {
        if (redoStack.Count == 0) return;

        ActionRecord action = redoStack.Pop();
        strokes.Add(action.Stroke);
        action.Stroke.lineObject.SetActive(true);
        totalRedoCount++;
        Debug.Log("Redo performed");
    }

    /*SEND/BACKEND COMMUNICATION*/
    //sends individual stroke data as JSON
    async void SendStrokeData(StrokeData stroke)
    {
        var serializedPoints = stroke.points.ConvertAll(p => new Vector3Serializable(p));

        StrokePayload payload = new StrokePayload
        {
            uid = userId,
            color = colorName,
            duration = stroke.duration,
            bounds = ComputeBounds(stroke.points)
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

    //sends only bounding box info to reduce payload
    async void SendStrokeBoundingBox(StrokeData stroke)
    {
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (Vector3 p in stroke.points)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.z < min.z) min.z = p.z;

            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
            if (p.z > max.z) max.z = p.z;
        }

        StrokePayloadBoundingBox payload = new StrokePayloadBoundingBox
        {
            uid = userId,
            color = stroke.colorName,
            duration = stroke.duration,
            bounds = new BoundingBox(min, max)
        };

        string json = JsonUtility.ToJson(payload);
        Debug.Log("Sending stroke bounding box JSON: " + json);

        using (var client = new HttpClient())
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("http://localhost:5000/api/strokes", content);
                Debug.Log($"Stroke bounding box sent: {response.StatusCode}");
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending bounding box: " + e.Message);
            }
        }
    }

    //sends all strokes of current topic to server
    async void SendFullDrawing()
    {
        Debug.Log("🔥 SendFullDrawing CALLED for uid: " + userId);
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
                bounds = ComputeBounds(s.points)
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
            colorChangeCount = colorChangeCount,
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

    //sends all topics in session to /api/session endpoint
    //uses HttpClient and JSON serialization (JsonUtility.ToJson)
    async void SendFullSessionData()
    {
        SessionPayload payload = new SessionPayload
        {
            uid = userId,
            session = sessionData
        };

        string json = JsonUtility.ToJson(payload);
        Debug.Log("Sending session JSON:\n" + json);

        using (HttpClient client = new HttpClient())
        {
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("http://localhost:5000/api/session", content);
                Debug.Log("Session data sent. Status: " + response.StatusCode);
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending session data: " + e.Message);
            }
        }
    }

    /*STROKE WIDTH MANAGEMENT*/
    //-> allows user to adjust line width, keeps counts for analytics
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

    /*CURSOR MANAGEMENT*/
    // --> updates cursor depending on drawing/erasing mode or when hovering UI buttons
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

    /*TOPIC COMPLETION*/
    //called when user finishes a topic
    public void OnDone()
    {
        if (currentTopicIndex >= topics.Count)
        {
            Debug.LogWarning("No more topics left.");
            return;
        }

        SendFullDrawing();

        //collects drawing payload, even if there are no strokes
        DrawingPayload drawingPayload = CollectDrawingPayload();

        //stores TopicDrawingData in sessionData.
        TopicDrawingData topicData = new TopicDrawingData
        {
            topic = topics[currentTopicIndex],
            usedReference = withReference,
            drawing = drawingPayload
        };

        //add topic data to session
        sessionData.Add(topicData);

        //clear current drawing from scene
        ClearCurrentDrawing();

        //move to next topic
        currentTopicIndex++;

        if (currentTopicIndex < topics.Count)
        {
            //prompt next topic reference choice
            PromptReferenceChoice();
        }
        else //sends full session if done
        {
            Debug.Log("All topics completed. Sending session data...");
            SendFullSessionData();
        }
    }


    DrawingPayload CollectDrawingPayload()
    {
        return new DrawingPayload
        {
            uid = userId,
            totalDuration = strokes.Sum(s => s.duration),
            strokes = strokes.Count > 0
                        ? strokes.Select(s => new StrokePayload
                            {
                                uid = userId,
                                color = s.colorName,
                                duration = s.duration,
                                bounds = ComputeBounds(s.points)
                            }).ToList()
                        : new List<StrokePayload>(),  // empty list if no strokes
            colorChangeCount = colorChangeCount,
            eraseCount = totalEraseCount,
            undoCount = totalUndoCount,
            redoCount = totalRedoCount,
            increaseWidthCount = totalIncreaseWidthCount,
            decreaseWidthCount = totalDecreaseWidthCount
        };
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

    void ClearCurrentDrawing()
    {
        foreach (var stroke in strokes)
        {
            if (stroke.lineObject != null)
                Destroy(stroke.lineObject);
        }

        strokes.Clear();
        undoStack.Clear();
        redoStack.Clear();

        isDrawing = false;
    }

    void ResetTopicStats()
    {
        colorChangeCount = 0;
        totalEraseCount = 0;
        totalUndoCount = 0;
        totalRedoCount = 0;
        totalIncreaseWidthCount = 0;
        totalDecreaseWidthCount = 0;
    }

}




