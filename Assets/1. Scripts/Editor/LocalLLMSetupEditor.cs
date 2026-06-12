#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class LocalLLMSetupEditor
{
    private const string SettingsAssetPath = "Assets/5. Data/LocalLLM/LocalLLMSettings.asset";
    private const string StreamingAssetsFolder = "Assets/StreamingAssets";
    private const long ExpectedModelSizeBytes = 1929902336L;

    [MenuItem("Tools/Local LLM/Create Settings Asset")]
    public static void CreateSettingsAsset()
    {
        LocalLLMSettings settings = EnsureSettingsAsset();
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
        Debug.Log($"[LocalLLM] Settings asset 준비: {SettingsAssetPath}");
    }

    [MenuItem("Tools/Local LLM/Download Korean Model")]
    public static void DownloadKoreanModel()
    {
        LocalLLMSettings settings = EnsureSettingsAsset();
        string destinationPath = Path.Combine(StreamingAssetsFolder, settings.ModelFileName);

        if (File.Exists(destinationPath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Local LLM",
                "모델 파일이 이미 있습니다. 다시 다운로드할까요?",
                "다시 다운로드",
                "취소");

            if (!overwrite)
            {
                return;
            }
        }

        DownloadModel(settings.ModelDownloadUrl, destinationPath);
    }

    [MenuItem("Tools/Local LLM/Setup Scene Service")]
    public static void SetupSceneService()
    {
        LocalLLMSettings settings = EnsureSettingsAsset();

        LocalLLMService existing = Object.FindObjectOfType<LocalLLMService>();
        if (existing != null)
        {
            SerializedObject serializedService = new SerializedObject(existing);
            serializedService.FindProperty("settings").objectReferenceValue = settings;
            serializedService.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[LocalLLM] 기존 LocalLLMService에 Settings를 연결했습니다.");
            return;
        }

        GameObject serviceObject = new GameObject("LocalLLMService");
        LocalLLMService service = serviceObject.AddComponent<LocalLLMService>();
        SerializedObject serializedNewService = new SerializedObject(service);
        serializedNewService.FindProperty("settings").objectReferenceValue = settings;
        serializedNewService.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(serviceObject, "Create LocalLLMService");
        Selection.activeGameObject = serviceObject;
        Debug.Log("[LocalLLM] LocalLLMService 오브젝트를 씬에 추가했습니다.");
    }

    [MenuItem("Tools/Local LLM/Cleanup Broken StreamingAssets Files")]
    public static void CleanupBrokenStreamingAssetsFiles()
    {
        string streamingAssetsFullPath = Path.GetFullPath(StreamingAssetsFolder);
        if (!Directory.Exists(streamingAssetsFullPath))
        {
            Debug.Log("[LocalLLM] StreamingAssets 폴더가 없습니다.");
            return;
        }

        int removedCount = 0;
        foreach (string filePath in Directory.GetFiles(streamingAssetsFullPath, "*", SearchOption.TopDirectoryOnly))
        {
            string extension = Path.GetExtension(filePath);
            if (extension == ".download" || filePath.EndsWith(".gguf.download"))
            {
                string metaPath = filePath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }

                File.Delete(filePath);
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            AssetDatabase.Refresh();
        }

        Debug.Log($"[LocalLLM] 불완전한 임시 파일 {removedCount}개 정리 완료.");
    }

    [MenuItem("Tools/Local LLM/Verify Setup")]
    public static void VerifySetup()
    {
        LocalLLMSettings settings = AssetDatabase.LoadAssetAtPath<LocalLLMSettings>(SettingsAssetPath);
        if (settings == null)
        {
            Debug.LogWarning("[LocalLLM] Settings asset이 없습니다. Tools > Local LLM > Create Settings Asset 실행");
            return;
        }

        string modelPath = Path.Combine(StreamingAssetsFolder, settings.ModelFileName);
        bool modelExists = File.Exists(modelPath);
        bool serviceExists = Object.FindObjectOfType<LocalLLMService>() != null;

        Debug.Log(
            "[LocalLLM] Setup 상태\n" +
            $"- Settings: {(settings != null ? "OK" : "MISSING")}\n" +
            $"- Model ({settings.ModelFileName}): {(modelExists ? "OK" : "MISSING")}\n" +
            $"- Scene Service: {(serviceExists ? "OK" : "MISSING")}\n" +
            $"- LLMUnity Package: {(IsLLMUnityInstalled() ? "OK" : "MISSING")}");
    }

    private static LocalLLMSettings EnsureSettingsAsset()
    {
        LocalLLMSettings settings = AssetDatabase.LoadAssetAtPath<LocalLLMSettings>(SettingsAssetPath);
        if (settings != null)
        {
            return settings;
        }

        if (!AssetDatabase.IsValidFolder("Assets/5. Data/LocalLLM"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/5. Data"))
            {
                AssetDatabase.CreateFolder("Assets", "5. Data");
            }

            AssetDatabase.CreateFolder("Assets/5. Data", "LocalLLM");
        }

        settings = ScriptableObject.CreateInstance<LocalLLMSettings>();
        AssetDatabase.CreateAsset(settings, SettingsAssetPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    private static bool IsLLMUnityInstalled()
    {
        return System.Type.GetType("LLMUnity.LLM, LLMUnity") != null;
    }

    private static void DownloadModel(string url, string assetDestinationPath)
    {
        EnsureStreamingAssetsFolder();

        string destinationFullPath = Path.GetFullPath(assetDestinationPath);
        string tempPath = Path.Combine(Application.temporaryCachePath, Path.GetFileName(destinationFullPath) + ".tmp");

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        EditorUtility.DisplayProgressBar("Local LLM", "모델 다운로드 준비 중...", 0f);

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.downloadHandler = new DownloadHandlerFile(tempPath);

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            EditorUtility.DisplayProgressBar(
                "Local LLM",
                "Qwen2.5-3B-Korean 모델 다운로드 중... (~1.8GB)",
                operation.progress);
        }

        EditorUtility.ClearProgressBar();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[LocalLLM] 모델 다운로드 실패: {request.error}");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            return;
        }

        FileInfo downloadedFile = new FileInfo(tempPath);
        if (downloadedFile.Length != ExpectedModelSizeBytes)
        {
            Debug.LogError(
                $"[LocalLLM] 다운로드 크기가 올바르지 않습니다. " +
                $"expected={ExpectedModelSizeBytes}, actual={downloadedFile.Length}");
            File.Delete(tempPath);
            return;
        }

        try
        {
            AssetDatabase.StartAssetEditing();

            if (File.Exists(destinationFullPath))
            {
                File.Delete(destinationFullPath);
            }

            File.Move(tempPath, destinationFullPath);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[LocalLLM] 모델 다운로드 완료: {assetDestinationPath}");
        EditorUtility.DisplayDialog(
            "Local LLM",
            "모델 다운로드가 완료되었습니다.\nTools > Local LLM > Setup Scene Service 를 실행해 씬에 서비스를 추가하세요.",
            "확인");
    }

    private static void EnsureStreamingAssetsFolder()
    {
        if (!AssetDatabase.IsValidFolder(StreamingAssetsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "StreamingAssets");
        }
    }
}
#endif
