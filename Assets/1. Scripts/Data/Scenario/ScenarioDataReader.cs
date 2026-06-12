using System;
using System.Collections.Generic;
using GoogleSheetsToUnity;
using UnityEngine;

[CreateAssetMenu(fileName = "ScenarioDataReader", menuName = "Data/Scenario Data Reader", order = 1)]
public class ScenarioDataReader : DataReaderBase
{
    private const string ExportFolder = "Assets/5. Data/Scenario";

    /// <summary>시트 1행 헤더 열 (A~N). 에디터 API 읽기 범위와 동일하게 유지.</summary>
    public static readonly string[] SheetColumnHeaders =
    {
        "Id", "Text", "Trigger", "Next", "Emotion", "Character", "Map",
        "C1Label", "C1Next", "C2Label", "C2Next", "C3Label", "C3Next",
        "Situation"
    };

    public const string SpreadsheetDataRangeEnd = "N200";

    [Header("Imported Data")]
    [SerializeField] private List<ScenarioData> dataList = new List<ScenarioData>();

    public IReadOnlyList<ScenarioData> DataList => dataList;

    public void ClearData()
    {
        dataList.Clear();
    }

    public void AddRow(ScenarioData row)
    {
        dataList.Add(row);
    }

    public void ApplyToScenarioAsset(ScenarioAsset asset)
    {
        asset.scenarioId = AssociatedWorksheet;
        asset.nodes = new List<ScenarioData>(dataList);
    }

    public ScenarioAsset CreateScenarioAsset()
    {
        return CreateScenarioAsset(AssociatedWorksheet);
    }

    public ScenarioAsset CreateScenarioAsset(string assetName)
    {
        if (dataList.Count == 0)
        {
            Debug.LogWarning("[ScenarioDataReader] 저장할 데이터가 없습니다. 먼저 시트를 읽어주세요.");
            return null;
        }

#if UNITY_EDITOR
        string safeName = SanitizeAssetName(string.IsNullOrWhiteSpace(assetName) ? AssociatedWorksheet : assetName);
        string folderPath = ExportFolder;
        string assetPath = $"{folderPath}/{safeName}.asset";

        EnsureFolderExists(folderPath);

        ScenarioAsset existing = UnityEditor.AssetDatabase.LoadAssetAtPath<ScenarioAsset>(assetPath);
        if (existing != null)
        {
            ApplyToScenarioAsset(existing);
            UnityEditor.EditorUtility.SetDirty(existing);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[ScenarioDataReader] Scenario 갱신: {assetPath}");
            return existing;
        }

        ScenarioAsset asset = CreateInstance<ScenarioAsset>();
        ApplyToScenarioAsset(asset);
        UnityEditor.AssetDatabase.CreateAsset(asset, assetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[ScenarioDataReader] Scenario 생성: {assetPath}");
        return asset;
#else
        Debug.LogWarning("[ScenarioDataReader] Scenario SO 저장은 에디터에서만 가능합니다.");
        return null;
#endif
    }

    internal static ScenarioData ParseRow(List<GSTU_Cell> cells)
    {
        string id = GetColumnValue(cells, "Id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new ScenarioData
        {
            id = id.Trim(),
            text = GetColumnValue(cells, "Text"),
            trigger = ParseTrigger(GetColumnValue(cells, "Trigger")),
            nextId = GetColumnValue(cells, "Next"),
            motionType = ParseEmotionType(GetColumnValue(cells, "Emotion")),
            characterType = ParseCharacterType(GetColumnValue(cells, "Character")),
            backgroundType = ParseBackgroundType(GetColumnValue(cells, "Map")),
            situation = GetColumnValue(cells, "Situation"),
            c1Label = GetColumnValue(cells, "C1Label"),
            c1NextId = GetColumnValue(cells, "C1Next"),
            c2Label = GetColumnValue(cells, "C2Label"),
            c2NextId = GetColumnValue(cells, "C2Next"),
            c3Label = GetColumnValue(cells, "C3Label"),
            c3NextId = GetColumnValue(cells, "C3Next")
        };
    }

    public static void ImportSpreadsheet(ScenarioDataReader reader, GstuSpreadSheet spreadsheet)
    {
        reader.ClearData();

        int startRow = reader.StartRowLength;
        int endRow = reader.EndRowLength;
        if (endRow < 0)
        {
            endRow = GetMaxRow(spreadsheet);
        }

        for (int row = startRow; row <= endRow; row++)
        {
            if (!spreadsheet.rows.primaryDictionary.ContainsKey(row))
            {
                continue;
            }

            ScenarioData data = ParseRow(spreadsheet.rows[row]);
            if (data != null)
            {
                reader.AddRow(data);
            }
        }
    }

    private static int GetMaxRow(GstuSpreadSheet spreadsheet)
    {
        int maxRow = 0;
        foreach (int row in spreadsheet.rows.primaryDictionary.Keys)
        {
            if (row > maxRow)
            {
                maxRow = row;
            }
        }

        return maxRow;
    }

    private static string GetColumnValue(List<GSTU_Cell> cells, string columnId)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (string.Equals(cells[i].columnId, columnId, StringComparison.OrdinalIgnoreCase))
            {
                return cells[i].value?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static DialogTrigger ParseTrigger(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DialogTrigger.None;
        }

        if (Enum.TryParse(raw.Trim(), true, out DialogTrigger trigger))
        {
            return trigger;
        }

        Debug.LogWarning($"[ScenarioDataReader] 알 수 없는 Trigger: {raw} → None 처리");
        return DialogTrigger.None;
    }

    private static EmotionType ParseEmotionType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return EmotionType.Default;
        }

        if (Enum.TryParse(raw.Trim(), true, out EmotionType emotionType))
        {
            return emotionType;
        }

        Debug.LogWarning($"[ScenarioDataReader] 알 수 없는 Emotion: {raw} → Default 처리");
        return EmotionType.Default;
    }

    private static CharacterType ParseCharacterType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return CharacterType.None;
        }

        if (Enum.TryParse(raw.Trim(), true, out CharacterType characterType))
        {
            return characterType;
        }

        Debug.LogWarning($"[ScenarioDataReader] 알 수 없는 Character: {raw} → None 처리");
        return CharacterType.None;
    }

    private static BackgroundType ParseBackgroundType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return BackgroundType.None;
        }

        if (Enum.TryParse(raw.Trim(), true, out BackgroundType backgroundType))
        {
            return backgroundType;
        }

        Debug.LogWarning($"[ScenarioDataReader] 알 수 없는 Map: {raw} → None 처리");
        return BackgroundType.None;
    }

#if UNITY_EDITOR
    private static void EnsureFolderExists(string folderPath)
    {
        if (UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!UnityEditor.AssetDatabase.IsValidFolder(next))
            {
                UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string SanitizeAssetName(string name)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "Scenario" : name.Trim();
    }
#endif
}
