using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; }
}

