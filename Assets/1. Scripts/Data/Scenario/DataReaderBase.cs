using UnityEngine;

public abstract class DataReaderBase : ScriptableObject
{
    [Header("Sheet Settings")]
    [Tooltip("스프레드시트 URL의 /d/ 와 /edit 사이 ID")]
    [SerializeField] private string associatedSheet = "";

    [Tooltip("읽을 워크시트(탭) 이름. 예: Scenario1")]
    [SerializeField] private string associatedWorksheet = "";

    [Header("Row Range")]
    [Tooltip("데이터 시작 행 (1행=헤더, 2행부터 데이터)")]
    [SerializeField] private int startRowLength = 2;

    [Tooltip("데이터 끝 행. -1이면 시트 끝까지")]
    [SerializeField] private int endRowLength = -1;

    public string AssociatedSheet => associatedSheet;
    public string AssociatedWorksheet => associatedWorksheet;
    public int StartRowLength => startRowLength;
    public int EndRowLength => endRowLength;
}
