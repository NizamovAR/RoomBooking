var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var rooms = new List<Room>
{
    new(1, "Переговорка А", 6),
};
var nextRoomId = 2;
app.MapGet("/rooms", () => rooms);

app.MapGet("/rooms/{id}", (int id) =>
{
    var room = rooms.FirstOrDefault(r => r.Id == id);
    return room is null ? Results.NotFound() : Results.Ok(room);
});

app.MapPost("/rooms", (Room room) =>
{
    if (string.IsNullOrWhiteSpace(room.Name) || room.Capacity <= 0)
    {
        return Results.BadRequest("Error");
    }

    var created = room with { Id = nextRoomId++ };
    rooms.Add(created);
    return Results.Created($"/rooms/{created.Id}", created);

});
app.Run();