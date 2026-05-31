using UnityEngine;

public class TestTextBallon : MonoBehaviour
{
    [SerializeField] private UI_TextDialog textBalloon;

    private readonly string[] lines =
    {
        "Hello, How Are You",
        "Fine Thank you And You",
        "Me too"
    };

    private int lineIndex;

    private void Start()
    {
        if (textBalloon == null)
        {
            Debug.LogWarning("[TestTextBallon] textBalloon is not assigned.");
            return;
        }

        textBalloon.SetAutoHideOnClick(false);
        textBalloon.OnTextBalloonClicked += OnBalloonClicked;

        lineIndex = 0;
        
        textBalloon.ShowText(lines[lineIndex]);
    }

    private void OnDestroy()
    {
        if (textBalloon != null)
        {
            textBalloon.OnTextBalloonClicked -= OnBalloonClicked;
        }
    }

    private void OnBalloonClicked(string _)
    {
        lineIndex++;

        if (lineIndex < lines.Length)
        {
            textBalloon.ShowText(lines[lineIndex]);
        }
        else
        {
            textBalloon.Hide();
        }
    }
}
