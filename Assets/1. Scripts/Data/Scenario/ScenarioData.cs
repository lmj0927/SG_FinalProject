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
    public CharacterType characterType = CharacterType.None;
    public BackgroundType backgroundType = BackgroundType.None;

    [Tooltip("LLM 되비춤/CBT용 장면 설명 (2~3문장). 예: 국어 시간.\\n친구들 앞에서 3분 발표를 해야 한다.\\n발표 시작 직전이다. 비우면 Map으로 추정합니다.")]
    [TextArea(3, 6)]
    public string situation = string.Empty;

    public string c1Label;
    public string c1NextId;
    public string c2Label;
    public string c2NextId;
    public string c3Label;
    public string c3NextId;

    public string ResolveTherapySituation()
    {
        if (!string.IsNullOrWhiteSpace(situation))
        {
            return situation.Trim();
        }

        return DescribeMapAsTherapySituation(backgroundType);
    }

    private static string DescribeMapAsTherapySituation(BackgroundType map)
    {
        switch (map)
        {
            case BackgroundType.Classroom:
                return "국어 시간.\n친구들 앞에서 발표를 해야 한다.\n발표 시작 직전이다.";
            case BackgroundType.Cafeteria:
                return "점심시간.\n친한 친구가 아직 오지 않았다.\n혼자 식판을 들고 자리를 찾아야 한다.";
            case BackgroundType.Hallway:
                return "복도에서 친한 친구 무리를 마주쳤다.\n인사할지 지나갈지 고민 중이다.";
            case BackgroundType.Playground:
                return "쉬는 시간.\n별로 친하지 않은 친구가 먼저 말을 걸었다.";
            case BackgroundType.Library:
                return "도서관.\n조용한 공간에서 다른 학생들 시선이 느껴진다.";
            case BackgroundType.Canteen:
                return "매점.\n친구들 사이에 끼어 말을 걸어야 한다.";
            case BackgroundType.SchoolGate:
                return "등교 시간.\n많은 학생들이 지켜보는 교문 앞이다.";
            case BackgroundType.JoomGate:
                return "줌인 게이트.\n처음 보는 사람들 앞에서 자기소개를 해야 한다.";
            case BackgroundType.TeacherRoom:
                return "교무실.\n선생님과 둘이 이야기해야 한다.";
            default:
                return "학교.\n사람들 앞에서 불안한 상황이다.";
        }
    }
}

public enum DialogTrigger
{
    None,
    Choice3,
    End,
    Enter,
}

public enum CharacterType
{
    None,
    Player,
    SY,
    HJ,
    SJ,
    Teacher,
    Fairy,
    Story,
}

public static class CharacterDisplay
{
    public static bool ShouldShowName(CharacterType characterType)
    {
        return characterType != CharacterType.None && characterType != CharacterType.Story;
    }

    public static string GetDisplayName(CharacterType characterType)
    {
        switch (characterType)
        {
            case CharacterType.Player: return "나";
            case CharacterType.SY: return "이서연";
            case CharacterType.HJ: return "이하진";
            case CharacterType.SJ: return "주석진";
            case CharacterType.Teacher: return "박선생님";
            case CharacterType.Fairy: return "요정";
            default: return string.Empty;
        }
    }
}

public enum BackgroundType
{
    None,
    Classroom,
    Cafeteria,
    Hallway,
    TeacherRoom,
    Playground,
    SchoolGate,
    JoomGate,
    Library,
    Canteen, //매점점
}