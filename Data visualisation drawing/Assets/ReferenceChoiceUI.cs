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
        panel.SetActive(false); //hides UI panel initially
        drawManager = FindFirstObjectByType<DrawManagerInput>(); //lets call functions on the drawing manager
    }

    //called when a topic is selected
    public void ShowTopic(string topic)
    {
        panel.SetActive(true); //shows panel
        topicText.text = "Draw: " + topic; //updates topic to display

        //hide image until user chooses
        referenceImageDisplay.gameObject.SetActive(false);
    }

    //called when user chooses to draw freely
    public void OnDrawFreely()
    {
        panel.SetActive(false); //hides panel
        drawManager.ShowReference(false); //tells manager not to show reference image
    }

    //called when user chooses to draw with a reference
    public void OnDrawWithReference()
    {
        panel.SetActive(false); //hides panel
        drawManager.ShowReference(true); //tells manager to show reference image
    }

}

