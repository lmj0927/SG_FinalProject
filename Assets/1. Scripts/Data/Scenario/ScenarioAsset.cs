using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Scenario", menuName = "Data/Scenario", order = 0)]
public class ScenarioAsset : ScriptableObject
{
    public string scenarioId;

    [Tooltip("CharacterImageManager에 등록된 캐릭터 키. 시트가 아닌 이 SO에서 직접 입력")]
    public string characterId;
    public List<ScenarioData> nodes = new List<ScenarioData>();
}
