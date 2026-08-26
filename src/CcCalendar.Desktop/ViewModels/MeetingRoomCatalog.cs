namespace CcCalendar.Desktop.ViewModels;

/// <summary>
/// 本地可预订的会议室列表；会议室后续增减时只需更新此处。
/// </summary>
public static class MeetingRoomCatalog
{
    public static readonly IReadOnlyList<string> Rooms =
    [
        "项目组二楼会议室",
        "生产组会议室",
        "技术中心二楼会议室",
        "聚英堂会议室",
        "院士办会议室",
        "研发中心三楼会议室",
        "财务部三楼会议室",
        "采购部会议室",
        "雅典学院",
        "蒙娜丽莎大厦",
    ];

    /// <summary>下拉选项：首项为空（不设置会议室），后接全部会议室。</summary>
    public static readonly IReadOnlyList<string> SelectionOptions = [string.Empty, .. Rooms];

    /// <summary>把下拉选择转换为日程地点；空选择返回 null（不设置会议室）。</summary>
    public static string? NormalizeSelection(string? selection)
    {
        string trimmed = selection?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? null : trimmed;
    }
}
