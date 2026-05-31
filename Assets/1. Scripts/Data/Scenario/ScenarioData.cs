using System;
using UnityEngine;

[Serializable]
public class ScenarioData
{
    public string id;
    [TextArea] public string text;
    public DialogTrigger trigger = DialogTrigger.None;
    public string nextId;

    public EmotionType motionType = EmotionType.Default;

    public string c1Label;
    public string c1NextId;
    public string c2Label;
    public string c2NextId;
    public string c3Label;
    public string c3NextId;
}
