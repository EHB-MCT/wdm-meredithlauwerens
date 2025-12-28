using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReferenceChoiceUI : MonoBehaviour
{
    public TextMeshProUGUI topicText;
    public Image referenceImageDisplay;
    public GameObject panel;

    private DrawManagerInput drawManager;

    void Awake()
    {
        panel.SetActive(false);
        drawManager = FindFirstObjectByType<DrawManagerInput>();
    }

    public void ShowTopic(string topic)
    {
        panel.SetActive(true);
        topicText.text = "Draw: " + topic;

        //hide image until user chooses
        referenceImageDisplay.gameObject.SetActive(false);
    }

    public void OnDrawFreely()
    {
        panel.SetActive(false);
        drawManager.ShowReference(false);
    }

    public void OnDrawWithReference()
    {
        panel.SetActive(false);
        drawManager.ShowReference(true);
    }

}

