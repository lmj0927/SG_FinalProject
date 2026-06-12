using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Scenario", menuName = "Data/Scenario", order = 0)]
public class ScenarioAsset : ScriptableObject
{
    public string scenarioId;
    public List<ScenarioData> nodes = new List<ScenarioData>();
}
