using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class MeetingRoomCatalogTests
{
    [Fact]
    public void CatalogContainsTheConfiguredMeetingRoomsInOrder()
    {
        Assert.Equal(
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
        ],
            MeetingRoomCatalog.Rooms);
    }

    [Fact]
    public void RoomSelectionOptionsLeadWithAnEmptyChoice()
    {
        IReadOnlyList<string> options = MeetingRoomCatalog.SelectionOptions;

        Assert.Equal(string.Empty, options[0]);
        Assert.Equal("项目组二楼会议室", options[1]);
        Assert.Equal(MeetingRoomCatalog.Rooms.Count + 1, options.Count);
    }
}
