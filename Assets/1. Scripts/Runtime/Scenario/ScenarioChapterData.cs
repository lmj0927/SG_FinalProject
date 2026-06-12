using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SubScenarioEntry
{
    public CharacterType character;
    public BackgroundType location;
    public ScenarioAsset scenario;
}

[Serializable]
public class ChapterEntry
{
    public ScenarioAsset mainScenario;
    public BackgroundType mapStartAfterMain = BackgroundType.Classroom;
    public List<SubScenarioEntry> subScenarios = new List<SubScenarioEntry>();
}
