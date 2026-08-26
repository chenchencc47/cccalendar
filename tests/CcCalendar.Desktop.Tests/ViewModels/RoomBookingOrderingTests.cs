using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class RoomBookingOrderingTests
{
    [Fact]
    public void MovesRoomsWithBookingsToFrontWhilePreservingCatalogOrder()
    {
        IReadOnlyList<string> rooms =
        [
            "项目组二楼会议室",
            "生产组会议室",
            "技术中心二楼会议室",
            "研发中心三楼会议室",
        ];

        IReadOnlyList<string> ordered = RoomBookingOrdering.MoveBookedRoomsToFront(
            rooms,
            ["研发中心三楼会议室", "技术中心二楼会议室"]);

        Assert.Equal(
            [
                "技术中心二楼会议室",
                "研发中心三楼会议室",
                "项目组二楼会议室",
                "生产组会议室",
            ],
            ordered);
    }

    [Fact]
    public void IgnoresBookingsForRoomsOutsideCatalog()
    {
        IReadOnlyList<string> ordered = RoomBookingOrdering.MoveBookedRoomsToFront(
            ["A", "B"],
            ["不存在"]);

        Assert.Equal(["A", "B"], ordered);
    }
}
