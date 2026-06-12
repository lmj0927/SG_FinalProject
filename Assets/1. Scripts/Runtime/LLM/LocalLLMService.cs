using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
using LLMUnity;
using UnityEngine;

/// <summary>
/// 프로젝트에 내장된 로컬 LLM(Qwen2.5-3B-Korean)을 관리합니다.
/// StreamingAssets/qwen25-3b-korean-Q4_K_M.gguf 를 사용합니다.
/// </summary>
public class LocalLLMService : MonoBehaviour
{
    public static LocalLLMService Instance { get; private set; }

    [SerializeField] private LocalLLMSettings settings;
    [SerializeField] private bool warmupOnStart = true;
    [SerializeField] private bool logInitialization = true;
    [SerializeField] private bool logEchoDebug = true;
    [SerializeField] private bool logCbtDebug = true;

    private LLM llm;
    private LLMAgent llmEchoAgent;
    private LLMAgent llmCbtAgent;
    private bool isInitialized;
    private bool isInitializing;
    private string lastInitError;
    private Task initializationTask;

    public bool IsReady => isInitialized;
    public bool IsInitializing => isInitializing;
    public string LastInitError => lastInitError;

    public static bool IsModelFilePresent(LocalLLMSettings targetSettings)
    {
        if (targetSettings == null)
        {
            return false;
        }

        string path = Path.Combine(Application.streamingAssetsPath, targetSettings.ModelFileName);
        return File.Exists(path);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        initializationTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (isInitialized || isInitializing)
        {
            return;
        }

        isInitializing = true;
        lastInitError = null;

        try
        {
            if (settings == null)
            {
                lastInitError = "LocalLLMSettings가 연결되지 않았습니다.";
                Debug.LogError($"[LocalLLMService] {lastInitError}");
                return;
            }

            if (!IsModelFilePresent(settings))
            {
                lastInitError =
                    $"모델 파일이 없습니다: {Path.Combine(Application.streamingAssetsPath, settings.ModelFileName)}";
                Debug.LogError(
                    $"[LocalLLMService] {lastInitError}\n" +
                    "Unity 메뉴 Tools > Local LLM > Download Korean Model 을 실행하세요.");
                return;
            }

            if (logInitialization)
            {
                Debug.Log($"[LocalLLMService] 초기화 시작: {settings.ModelFileName}");
            }

            gameObject.SetActive(false);

            llm = gameObject.AddComponent<LLM>();
            llm.model = settings.ModelFileName;
            llm.numThreads = settings.NumThreads;
            llm.numGPULayers = settings.NumGpuLayers;

            llmEchoAgent = CreateAgent(
                settings.SystemPrompt,
                settings.EchoTemperature,
                settings.EchoMaxTokens);
            llmCbtAgent = CreateAgent(
                settings.CbtSystemPrompt,
                settings.CbtTemperature,
                settings.CbtMaxTokens);

            gameObject.SetActive(true);

            if (logInitialization)
            {
                Debug.Log("[LocalLLMService] LLMManager/모델 셋업 대기 중...");
            }

            bool setupSucceeded = await LLM.WaitUntilModelSetup();
            if (!setupSucceeded)
            {
                lastInitError = "LLMManager 모델 셋업 실패 (LLM.modelSetupFailed)";
                Debug.LogError($"[LocalLLMService] {lastInitError}");
                return;
            }

            if (logInitialization)
            {
                Debug.Log("[LocalLLMService] LLM 서버 시작 대기 중... (모델 로딩, 수십 초~수 분 소요 가능)");
            }

            await llm.WaitUntilReady();

            if (llm.failed || !llm.started)
            {
                lastInitError = $"LLM 서버 시작 실패 (started={llm.started}, failed={llm.failed})";
                Debug.LogError($"[LocalLLMService] {lastInitError}");
                return;
            }

            if (warmupOnStart)
            {
                if (logInitialization)
                {
                    Debug.Log("[LocalLLMService] Warmup 시작...");
                }

                await llmEchoAgent.Warmup();
            }

            isInitialized = true;

            if (logInitialization)
            {
                Debug.Log($"[LocalLLMService] 로컬 LLM 준비 완료: {settings.ModelFileName}");
            }
        }
        catch (Exception exception)
        {
            lastInitError = exception.Message;
            Debug.LogError($"[LocalLLMService] 초기화 실패: {exception.Message}\n{exception.StackTrace}");
        }
        finally
        {
            isInitializing = false;
        }
    }

    public async Task WaitUntilReadyAsync()
    {
        if (initializationTask != null)
        {
            await initializationTask;
        }
    }

    /// <summary>
    /// 환자 답변을 치료용 되비춤 문장으로 변환합니다. 실패 시 원문을 반환합니다.
    /// </summary>
    public async Task<string> GenerateEchoAsync(
        string question,
        string userAnswer,
        string therapySituation,
        bool verbose = false)
    {
        bool shouldLog = verbose || logEchoDebug;
        var stopwatch = Stopwatch.StartNew();

        if (shouldLog)
        {
            Debug.Log(
                "[LocalLLMService][Echo] GenerateEchoAsync 시작\n" +
                $"- IsReady(before wait): {isInitialized}\n" +
                $"- IsInitializing: {isInitializing}\n" +
                $"- Model: {settings?.ModelFileName ?? "(none)"}");
        }

        await WaitUntilReadyAsync();
        stopwatch.Stop();

        if (shouldLog)
        {
            Debug.Log(
                "[LocalLLMService][Echo] WaitUntilReadyAsync 완료\n" +
                $"- ElapsedMs: {stopwatch.ElapsedMilliseconds}\n" +
                $"- IsReady: {isInitialized}\n" +
                $"- llmEchoAgent null: {llmEchoAgent == null}");
        }

        if (string.IsNullOrWhiteSpace(userAnswer))
        {
            if (shouldLog)
            {
                Debug.LogWarning("[LocalLLMService][Echo] userAnswer가 비어 있어 원문을 반환합니다.");
            }

            return userAnswer;
        }

        if (!isInitialized || llmEchoAgent == null)
        {
            if (shouldLog)
            {
                Debug.LogWarning(
                    "[LocalLLMService][Echo] LLM이 준비되지 않아 원문을 반환합니다.\n" +
                    $"- isInitialized: {isInitialized}\n" +
                    $"- llmEchoAgent null: {llmEchoAgent == null}\n" +
                    $"- llm started: {llm != null && llm.started}\n" +
                    $"- llm failed: {llm != null && llm.failed}\n" +
                    $"- settings null: {settings == null}\n" +
                    $"- model exists: {settings != null && IsModelFilePresent(settings)}\n" +
                    $"- lastInitError: {lastInitError ?? "(none)"}");
            }

            return userAnswer;
        }

        try
        {
            string prompt = BuildEchoPrompt(question, userAnswer, therapySituation);

            if (shouldLog)
            {
                Debug.Log(
                    "[LocalLLMService][Echo] Chat 요청\n" +
                    $"- Situation: \"{therapySituation}\"\n" +
                    $"- Question: \"{question}\"\n" +
                    $"- UserAnswer: \"{userAnswer}\"\n" +
                    $"- Prompt:\n{prompt}");
            }

            stopwatch.Restart();
            string reply = await llmEchoAgent.Chat(prompt, addToHistory: false);
            stopwatch.Stop();

            string cleaned = NormalizeEchoTone(CleanReply(reply, userAnswer));
            if (!IsValidEcho(cleaned, userAnswer))
            {
                string fallbackLine = BuildCounselorFallback(userAnswer);
                if (shouldLog)
                {
                    Debug.LogWarning(
                        "[LocalLLMService][Echo] LLM 출력이 형식에 맞지 않아 fallback 대사로 대체합니다.\n" +
                        $"- LLM: \"{cleaned}\"\n" +
                        $"- Fallback: \"{fallbackLine}\"");
                }

                cleaned = fallbackLine;
            }

            if (shouldLog)
            {
                Debug.Log(
                    "[LocalLLMService][Echo] Chat 응답\n" +
                    $"- ElapsedMs: {stopwatch.ElapsedMilliseconds}\n" +
                    $"- RawReply: \"{reply}\"\n" +
                    $"- Final: \"{cleaned}\"");
            }

            return cleaned;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[LocalLLMService][Echo] 되비춤 생성 실패, 원문 사용: {exception.Message}\n{exception.StackTrace}");
            return userAnswer;
        }
    }

    /// <summary>
    /// CBT 기반 대안 사고·행동 제안 대사를 생성합니다. 실패 시 fallback 문장을 반환합니다.
    /// </summary>
    public async Task<string> GenerateCbtAsync(
        string question,
        string userAnswer,
        string echoLine,
        string therapySituation,
        bool verbose = false)
    {
        bool shouldLog = verbose || logCbtDebug;
        var stopwatch = Stopwatch.StartNew();

        await WaitUntilReadyAsync();

        if (string.IsNullOrWhiteSpace(userAnswer))
        {
            return string.Empty;
        }

        if (!isInitialized || llmCbtAgent == null)
        {
            if (shouldLog)
            {
                Debug.LogWarning("[LocalLLMService][CBT] LLM이 준비되지 않아 fallback을 반환합니다.");
            }

            return BuildCbtFallback(userAnswer, echoLine, therapySituation);
        }

        try
        {
            string prompt = BuildCbtPrompt(question, userAnswer, echoLine, therapySituation);

            if (shouldLog)
            {
                Debug.Log(
                    "[LocalLLMService][CBT] Chat 요청\n" +
                    $"- Situation: \"{therapySituation}\"\n" +
                    $"- Question: \"{question}\"\n" +
                    $"- UserAnswer: \"{userAnswer}\"\n" +
                    $"- Echo: \"{echoLine}\"\n" +
                    $"- Prompt:\n{prompt}");
            }

            stopwatch.Restart();
            string reply = await llmCbtAgent.Chat(prompt, addToHistory: false);
            stopwatch.Stop();

            string cleaned = NormalizeCbtTone(CleanCbtReply(reply));
            if (!IsValidCbt(cleaned, userAnswer))
            {
                string fallbackLine = BuildCbtFallback(userAnswer, echoLine, therapySituation);
                if (shouldLog)
                {
                    Debug.LogWarning(
                        "[LocalLLMService][CBT] LLM 출력이 형식에 맞지 않아 fallback으로 대체합니다.\n" +
                        $"- LLM: \"{cleaned}\"\n" +
                        $"- Fallback: \"{fallbackLine}\"");
                }

                cleaned = fallbackLine;
            }

            if (shouldLog)
            {
                Debug.Log(
                    "[LocalLLMService][CBT] Chat 응답\n" +
                    $"- ElapsedMs: {stopwatch.ElapsedMilliseconds}\n" +
                    $"- RawReply: \"{reply}\"\n" +
                    $"- Final: \"{cleaned}\"");
            }

            return cleaned;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[LocalLLMService][CBT] 생성 실패, fallback 사용: {exception.Message}\n{exception.StackTrace}");
            return BuildCbtFallback(userAnswer, echoLine, therapySituation);
        }
    }

    private LLMAgent CreateAgent(string systemPrompt, float temperature, int maxTokens)
    {
        LLMAgent agent = gameObject.AddComponent<LLMAgent>();
        agent.llm = llm;
        agent.systemPrompt = systemPrompt;
        agent.temperature = temperature;
        agent.numPredict = maxTokens;
        agent.topP = 0.85f;
        return agent;
    }

    private string BuildEchoPrompt(string question, string userAnswer, string therapySituation)
    {
        string fewShot = settings != null ? settings.EchoFewShotPrompt : string.Empty;
        return
            $"{fewShot}\n현재\n" +
            $"[참고 상황]\n{therapySituation}\n\n" +
            $"질문: {question}\n" +
            $"답변: {userAnswer}\n" +
            "출력:";
    }

    private string BuildCbtPrompt(string question, string userAnswer, string echoLine, string therapySituation)
    {
        string fewShot = settings != null ? settings.CbtFewShotPrompt : string.Empty;
        return
            $"{fewShot}\n현재\n" +
            $"[참고 상황]\n{therapySituation}\n\n" +
            $"질문: {question}\n" +
            $"답변: {userAnswer}\n" +
            $"되비춤: {echoLine}\n" +
            "1~4단계를 내부적으로 거친 뒤, 현실적 생각 1문장 + 행동 실험 1문장만 반말로 출력하라.\n" +
            "출력:";
    }

    private static bool IsValidEcho(string echo, string userAnswer)
    {
        if (string.IsNullOrWhiteSpace(echo))
        {
            return false;
        }

        string trimmed = echo.Trim();
        if (string.Equals(trimmed, userAnswer.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Length >= 8 && trimmed.Length <= 80;
    }

    private static string NormalizeEchoTone(string echo)
    {
        if (string.IsNullOrWhiteSpace(echo))
        {
            return echo;
        }

        string normalized = echo.Trim().TrimEnd('!', '?');
        normalized = Regex.Replace(normalized, @"^(?:그래|그랬어)\s*[,，]?\s*", "그랬구나. ");

        if (!normalized.StartsWith("그랬구나", StringComparison.Ordinal))
        {
            normalized = "그랬구나. " + normalized;
        }
        else if (!normalized.StartsWith("그랬구나.", StringComparison.Ordinal) &&
                 !normalized.StartsWith("그랬구나 ", StringComparison.Ordinal))
        {
            normalized = "그랬구나." + normalized.Substring("그랬구나".Length);
        }

        normalized = Regex.Replace(normalized, @"두려워요\.?$", "두렵구나.");
        normalized = Regex.Replace(normalized, @"무서워요\.?$", "두렵구나.");
        normalized = Regex.Replace(normalized, @"두렵구요\.?$", "두렵구나.");
        normalized = Regex.Replace(normalized, @"걱정돼요\.?$", "걱정되는구나.");
        normalized = Regex.Replace(normalized, @"걱정되요\.?$", "걱정되는구나.");
        normalized = Regex.Replace(normalized, @"불안해요\.?$", "불안한구나.");
        normalized = Regex.Replace(normalized, @"힘들어요\.?$", "힘든구나.");

        if (Regex.IsMatch(normalized, @"요\.?$"))
        {
            normalized = Regex.Replace(normalized, @"요\.?$", "구나.");
        }

        if (!Regex.IsMatch(normalized, @"구나[.!?]?$"))
        {
            normalized = normalized.TrimEnd('.') + "구나.";
        }

        return normalized.Trim();
    }

    private static string NormalizeCbtTone(string cbt)
    {
        if (string.IsNullOrWhiteSpace(cbt))
        {
            return cbt;
        }

        string normalized = cbt.Trim();

        (string pattern, string replacement)[] phraseReplacements =
        {
            (@"생각해 보도록 해요", "생각해 보도록 해봐"),
            (@"보도록 해요", "보도록 해봐"),
            (@"신경 쓰지 않을 거예요", "신경 안 쓸 거야"),
            (@"쓰지 않을 거예요", "안 쓸 거야"),
            (@"하지 않을 거예요", "안 할 거야"),
            (@"하지 말아요", "하지 마"),
            (@"말아요", "마"),
            (@"하지 마세요", "하지 마"),
            (@"하지 않아도 돼요", "하지 않아도 돼"),
            (@"해도 돼요", "해도 돼"),
            (@"집중해보세요", "집중해봐"),
            (@"표현해 보세요", "표현해봐"),
            (@"표현해 봐", "표현해봐"),
            (@"해보세요", "해봐"),
            (@"해 보세요", "해봐"),
            (@"할 거예요", "할 거야"),
            (@"거예요", "거야"),
            (@"이에요", "이야"),
            (@"예요", "야"),
            (@"돼요", "돼"),
            (@"해요", "해"),
        };

        foreach ((string pattern, string replacement) in phraseReplacements)
        {
            normalized = Regex.Replace(normalized, pattern, replacement);
        }

        normalized = Regex.Replace(normalized, @"당신의", "네");
        normalized = Regex.Replace(normalized, @"당신이", "네가");
        normalized = Regex.Replace(normalized, @"당신을", "너를");
        normalized = Regex.Replace(normalized, @"당신은", "너는");
        normalized = Regex.Replace(normalized, @"당신", "너");

        normalized = Regex.Replace(normalized, @",\s*[가-힣]{2,5}야\s*,", ", ");
        normalized = Regex.Replace(normalized, @"\s{2,}", " ");

        return normalized.Trim();
    }

    private static bool IsValidCbt(string cbtLine, string userAnswer)
    {
        if (string.IsNullOrWhiteSpace(cbtLine))
        {
            return false;
        }

        string trimmed = cbtLine.Trim();
        if (string.Equals(trimmed, userAnswer.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Length < 12 || trimmed.Length > 200)
        {
            return false;
        }

        if (LooksLikeSituationEcho(trimmed))
        {
            return false;
        }

        return true;
    }

    private static bool LooksLikeSituationEcho(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (Regex.IsMatch(text, @"^(?:\[?참고\s*상황\]?|상황)\s*[:：]"))
        {
            return true;
        }

        string[] offTopicSituationHints = { "직장", "회사", "상사", "면접" };
        foreach (string hint in offTopicSituationHints)
        {
            if (text.Contains(hint) && text.Length < 40)
            {
                return true;
            }
        }

        return false;
    }

    private static string CleanCbtReply(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return string.Empty;
        }

        string cleaned = reply.Trim().Trim('"', '\'', '“', '”', '`');
        cleaned = Regex.Replace(cleaned, @"^(?:출력|답변|답|결과|선생님)\s*[:：]\s*", string.Empty);
        cleaned = Regex.Replace(cleaned, @"^(?:\[?참고\s*상황\]?|상황)\s*[:：]\s*", string.Empty, RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^(?:질문|답변|되비춤)\s*[:：]\s*", string.Empty, RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\s*\r?\n\s*", " ");
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");

        return string.IsNullOrWhiteSpace(cleaned) ? string.Empty : cleaned.Trim();
    }

    private static string BuildCbtFallback(string userAnswer, string echoLine, string therapySituation)
    {
        bool presentationContext =
            (!string.IsNullOrWhiteSpace(therapySituation) && therapySituation.Contains("발표")) ||
            userAnswer.Contains("발표") ||
            userAnswer.Contains("떨리") ||
            userAnswer.Contains("비웃");

        string line;
        if (presentationContext)
        {
            line =
                "그럴 때는 교실 맨 뒤 시계나 게시판 벽지, 아니면 바로 앞 친구의 정수리나 미간을 보며 말해봐. " +
                "사람들 표정을 읽으려 하지 않아도 돼.";
        }
        else if (userAnswer.Contains("시선") || userAnswer.Contains("비난") || userAnswer.Contains("이상하게"))
        {
            line =
                "모두가 너를 비난하거나 이상하게 본다는 건 확인된 사실이 아니야. " +
                "발바닥 느낌에 잠깐 집중한 뒤, 한 문장만 또박또박 말해보자.";
        }
        else if (!string.IsNullOrWhiteSpace(echoLine))
        {
            line =
                $"그 마음 이해해. {echoLine} 하지만 그 생각이 사실과 같다고 단정하진 말자. " +
                "숨을 한번 고르고 지금 할 수 있는 말 한 문장부터 해보자.";
        }
        else
        {
            line =
                "지금 떠오른 걱정이 100% 일어날 일은 아닐 수 있어. " +
                "숨을 고르고, 눈에 보이는 중립적인 곳 하나를 정해 거기만 잠깐 보며 말해보자.";
        }

        return NormalizeCbtTone(line);
    }

    private static string BuildCounselorFallback(string userAnswer)
    {
        string fearCore = ExtractFearCore(userAnswer);
        string line;
        if (fearCore.Contains("할까") || fearCore.Contains("볼까"))
        {
            line = $"그랬구나. {fearCore} 두렵구나.";
        }
        else if (userAnswer.Contains("걱정"))
        {
            line = $"그랬구나. {fearCore} 걱정되는구나.";
        }
        else
        {
            line = $"그랬구나. {fearCore} 것 때문에 두렵구나.";
        }

        return NormalizeEchoTone(line);
    }

    private static string ExtractFearCore(string userAnswer)
    {
        string text = userAnswer.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return userAnswer;
        }

        MatchCollection fearMatches = Regex.Matches(text, @"([^,.!?]+?(?:할까\s?봐|할까봐|볼까\s?봐|볼까봐))");
        if (fearMatches.Count > 0)
        {
            string clause = fearMatches[fearMatches.Count - 1].Groups[1].Value.Trim();
            clause = Regex.Replace(clause, @"(?:할까\s?봐|할까봐|볼까\s?봐|볼까봐)$", "할까 봐").Trim();

            if (!string.IsNullOrWhiteSpace(clause))
            {
                return clause;
            }
        }

        string[] segments = text.Split(new[] { ',', '.', '!', '?', '，' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            string segment = segments[i].Trim();
            segment = Regex.Replace(segment, @"(?:두려워|무서워|걱정돼|걱정되|두렵고)$", string.Empty).Trim();
            segment = Regex.Replace(segment, @"(?:할까\s?봐|할까봐)$", string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(segment) && segment.Length <= 30)
            {
                return segment;
            }
        }

        return text.Length <= 30 ? text : text.Substring(0, 30).Trim();
    }

    private static string CleanReply(string reply, string fallback)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return fallback;
        }

        string cleaned = reply.Trim().Trim('"', '\'', '“', '”', '`');
        cleaned = Regex.Replace(cleaned, @"^(?:출력|답변|답|결과|선생님)\s*[:：]\s*", string.Empty);

        int newlineIndex = cleaned.IndexOf('\n');
        if (newlineIndex >= 0)
        {
            cleaned = cleaned.Substring(0, newlineIndex).Trim();
        }

        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
