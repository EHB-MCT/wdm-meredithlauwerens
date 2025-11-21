using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Belangrijk!
using UnityEngine.EventSystems;

using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;

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
    }

    [System.Serializable]
    public class StrokeData
    {
        public Color color;
        public string colorName;
        public float duration;
        public List<Vector3> points;
    }

    private string userId;
    private string colorName = "Black";


    void Start()
    {
        userId = System.Guid.NewGuid().ToString();
        Debug.Log($"User ID: {userId}");
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

    //input callbacks (connecting via PlayerInput)
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
        Debug.Log("Right click detected");
    }

    void Update()
    {
        if (isDrawing)
            DrawStroke();
    }

    void StartStroke()
    {
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
            StrokeData stroke = new()
            {
                color = drawColor,
                colorName = colorName,
                duration = Time.time - strokeStartTime,
                points = new List<Vector3>(points)
            };
            strokes.Add(stroke);
            SendStrokeData(stroke);
            Debug.Log($"Stroke saved: {stroke.points.Count} points, duration {stroke.duration:F2}s");
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
    }



    async void SendStrokeData(StrokeData stroke)
    {
        var serializedPoints = stroke.points.ConvertAll(p => new Vector3Serializable(p));

        StrokePayload payload = new StrokePayload
        {
            uid = userId,
            color = colorName,
            duration = stroke.duration,
            points = serializedPoints
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
                points = serializedPoints
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
            strokes = strokePayloads
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

}




