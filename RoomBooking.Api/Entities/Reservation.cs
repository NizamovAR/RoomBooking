namespace RoomBooking.Api.Entities;

public class Reservation
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public string UserName { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}