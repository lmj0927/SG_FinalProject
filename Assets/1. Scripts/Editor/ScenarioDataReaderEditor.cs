#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GoogleSheetsToUnity;
using GoogleSheetsToUnity.ThirdPary;
using TinyJSON;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

[CustomEditor(typeof(ScenarioDataReader))]
public class ScenarioDataReaderEditor : Editor
{
    private bool showImportedPreview = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var reader = (ScenarioDataReader)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "1행 헤더 (대소문자 무시):\n" +
            string.Join(", ", ScenarioDataReader.SheetColumnHeaders) +
            "\n\nTrigger: None, Choice3, End, Enter\n" +
            "Enter: 입력 후 Next로 이동.\n" +
            "  {input} = LLM 되비춤 대사 전체\n" +
            "  {cbt} = LLM CBT 대안 사고·행동 제안 대사 전체\n" +
            "  예) fear_echo:{input} → fear_cbt:{cbt}\n" +
            "Character: None, Player, SY, HJ, SJ, Teacher, Fairy, Story\n" +
            "Character None/Story는 이름·초상 미표시, Player는 이름만 표시\n" +
            "Map: None, Classroom, Cafeteria, Hallway, TeacherRoom, Playground, SchoolGate\n" +
            "Situation: LLM용 장면 설명 2~3문장 (Enter 노드). 예) 국어 시간. / 친구들 앞에서 3분 발표. / 발표 직전. 비우면 Map 기본 장면 사용\n" +
            "Emotion: Default, Happy, Sad, Angry, Surprised\n" +
            "Character None/Player는 초상 미표시, Map None은 배경 유지",
            MessageType.Info);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Google Sheet", EditorStyles.boldLabel);

        if (GUILayout.Button("데이터 읽기 (API 호출)", GUILayout.Height(28)))
        {
            reader.ClearData();
            ReadSpreadsheet(reader, OnSpreadsheetLoaded, OnSpreadsheetFailed);
        }

        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(reader.DataList.Count == 0))
        {
            if (GUILayout.Button("Scenario SO 저장", GUILayout.Height(28)))
            {
                reader.CreateScenarioAsset();
            }
        }

        if (SpreadsheetManager.Config != null
            && string.IsNullOrEmpty(SpreadsheetManager.Config.gdr.access_token))
        {
            EditorGUILayout.HelpBox(
                "OAuth 토큰이 비어 있습니다. Window → GSTU → Open Config → Build Connection 후 다시 시도하세요.",
                MessageType.Warning);
        }

        DrawImportedDataPreview(reader);
    }

    private void DrawImportedDataPreview(ScenarioDataReader reader)
    {
        IReadOnlyList<ScenarioData> rows = reader.DataList;
        if (rows.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space(8);
        showImportedPreview = EditorGUILayout.Foldout(showImportedPreview, $"Import 미리보기 ({rows.Count}행)", true);
        if (!showImportedPreview)
        {
            return;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < rows.Count; i++)
        {
            ScenarioData row = rows[i];
            if (row == null)
            {
                continue;
            }

            EditorGUILayout.LabelField(
                row.id,
                $"{row.backgroundType} | {row.characterType}/{row.motionType} | {row.trigger} → {row.nextId}",
                EditorStyles.miniLabel);
        }

        EditorGUI.indentLevel--;
    }

    private void OnSpreadsheetLoaded(GstuSpreadSheet spreadsheet)
    {
        var reader = (ScenarioDataReader)target;
        ScenarioDataReader.ImportSpreadsheet(reader, spreadsheet);
        EditorUtility.SetDirty(reader);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ScenarioDataReader] {reader.DataList.Count}개 행 import 완료 ({reader.AssociatedWorksheet})");
    }

    private static void OnSpreadsheetFailed(string message)
    {
        Debug.LogError($"[ScenarioDataReader] 시트 읽기 실패\n{message}");
    }

    private static void ReadSpreadsheet(
        ScenarioDataReader reader,
        UnityAction<GstuSpreadSheet> onSuccess,
        UnityAction<string> onError)
    {
        if (string.IsNullOrWhiteSpace(reader.AssociatedSheet))
        {
            onError?.Invoke("Associated Sheet ID가 비어 있습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(reader.AssociatedWorksheet))
        {
            onError?.Invoke("Associated Worksheet 이름이 비어 있습니다.");
            return;
        }

        var search = new GSTU_Search(
            reader.AssociatedSheet.Trim(),
            reader.AssociatedWorksheet.Trim(),
            "A1",
            ScenarioDataReader.SpreadsheetDataRangeEnd);

        EditorCoroutineRunner.StartCoroutine(ReadSpreadsheetCoroutine(search, onSuccess, onError));
    }

    private static IEnumerator ReadSpreadsheetCoroutine(
        GSTU_Search search,
        UnityAction<GstuSpreadSheet> onSuccess,
        UnityAction<string> onError)
    {
        yield return EditorCoroutineRunner.StartCoroutine(GoogleAuthrisationHelper.CheckForRefreshOfToken());

        string accessToken = SpreadsheetManager.Config.gdr.access_token;
        if (string.IsNullOrEmpty(accessToken))
        {
            onError?.Invoke(
                "Google access_token이 비어 있습니다.\n" +
                "Window → GSTU → Open Config → Build Connection 으로 다시 연결하세요.");
            yield break;
        }

        string valuesUrl = BuildValuesUrl(search);
        using (UnityWebRequest request = CreateAuthorizedGetRequest(valuesUrl, accessToken))
        {
            yield return request.SendWebRequest();

            string responseText = request.downloadHandler.text;

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (responseText.Contains("NOT_FOUND") || request.responseCode == 404)
                {
                    yield return EditorCoroutineRunner.StartCoroutine(
                        AppendNotFoundDiagnostics(search, accessToken, responseText, onError));
                }
                else
                {
                    onError?.Invoke($"HTTP 오류: {request.error}\nURL: {valuesUrl}\n{responseText}");
                }

                yield break;
            }

            if (string.IsNullOrEmpty(responseText) || responseText == "{}")
            {
                onError?.Invoke("시트 응답이 비어 있습니다.");
                yield break;
            }

            ValueRange rawData;
            try
            {
                rawData = JSON.Load(responseText).Make<ValueRange>();
            }
            catch (System.Exception e)
            {
                onError?.Invoke($"응답 파싱 실패: {e.Message}\n{responseText}");
                yield break;
            }

            if (rawData == null || string.IsNullOrEmpty(rawData.range) || !rawData.range.Contains(":"))
            {
                onError?.Invoke($"range 형식 오류: '{rawData?.range}'\n{responseText}");
                yield break;
            }

            if (rawData.values == null || rawData.values.Count == 0)
            {
                onError?.Invoke("시트에 데이터가 없습니다. 1행 헤더 + 2행부터 데이터를 확인하세요.");
                yield break;
            }

            var response = new GSTU_SpreadsheetResponce(rawData);
            onSuccess?.Invoke(new GstuSpreadSheet(response, search.titleColumn, search.titleRow));
        }
    }

    private static IEnumerator AppendNotFoundDiagnostics(
        GSTU_Search search,
        string accessToken,
        string apiResponse,
        UnityAction<string> onError)
    {
        var message = new StringBuilder();
        message.AppendLine("HTTP 404 NOT_FOUND");
        message.AppendLine($"Sheet ID: {search.sheetId}");
        message.AppendLine($"탭: {search.worksheetName}");
        message.AppendLine("→ Sheet ID / 탭 이름 / OAuth 계정 시트 공유를 확인하세요.");
        message.AppendLine();
        message.AppendLine(apiResponse);

        string metaUrl =
            $"https://sheets.googleapis.com/v4/spreadsheets/{search.sheetId}?fields=sheets.properties.title";

        using (UnityWebRequest metaRequest = CreateAuthorizedGetRequest(metaUrl, accessToken))
        {
            yield return metaRequest.SendWebRequest();

            if (metaRequest.result == UnityWebRequest.Result.Success)
            {
                List<string> tabNames = ParseSheetTabNames(metaRequest.downloadHandler.text);
                message.AppendLine();
                message.AppendLine("[진단] 접근 가능한 탭 목록:");
                for (int i = 0; i < tabNames.Count; i++)
                {
                    message.AppendLine($"  - \"{tabNames[i]}\"");
                }
            }
        }

        onError?.Invoke(message.ToString());
    }

    [System.Serializable]
    private class SheetsMetadataResponse
    {
        public SheetMetadataEntry[] sheets;
    }

    [System.Serializable]
    private class SheetMetadataEntry
    {
        public SheetProperties properties;
    }

    [System.Serializable]
    private class SheetProperties
    {
        public string title;
    }

    private static List<string> ParseSheetTabNames(string json)
    {
        var names = new List<string>();
        if (string.IsNullOrEmpty(json))
        {
            return names;
        }

        SheetsMetadataResponse metadata = JsonUtility.FromJson<SheetsMetadataResponse>(json);
        if (metadata?.sheets == null)
        {
            return names;
        }

        for (int i = 0; i < metadata.sheets.Length; i++)
        {
            string title = metadata.sheets[i]?.properties?.title;
            if (!string.IsNullOrEmpty(title))
            {
                names.Add(title);
            }
        }

        return names;
    }

    private static UnityWebRequest CreateAuthorizedGetRequest(string url, string accessToken)
    {
        var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", "Bearer " + accessToken);
        return request;
    }

    private static string BuildValuesUrl(GSTU_Search search)
    {
        string range = FormatA1Range(search.worksheetName, search.startCell, search.endCell);
        return
            "https://sheets.googleapis.com/v4/spreadsheets/" +
            search.sheetId +
            "/values/" +
            UnityWebRequest.EscapeURL(range);
    }

    private static string FormatA1Range(string worksheetName, string startCell, string endCell)
    {
        string escapedName = worksheetName.Replace("'", "''");
        return $"'{escapedName}'!{startCell}:{endCell}";
    }
}
#endif
