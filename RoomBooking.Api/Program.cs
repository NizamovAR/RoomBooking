using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext> (options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.MapGet("/rooms", async (AppDbContext db) => 
///AsNoTracking - для ускорения работы, просто чтение, без запоминания
    await db.Rooms.AsNoTracking().ToListAsync());

app.MapGet("/rooms/{id}", async (int id, AppDbContext db) =>
{
    var room = await db.Rooms.FindAsync(id);
    return room is null ? Results.NotFound() : Results.Ok(room);
});

app.MapPost("/rooms", async (CreateRoomRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Name is requered");

    if (request.Capacity <= 0) return Results.BadRequest("Capacity must be positive");
    var room = new Room { Name = request.Name, Capacity = request.Capacity};
    db.Rooms.Add(room);
    ///добавление собранных данных в базу
    await db.SaveChangesAsync();

    return Results.Created($"/rooms/{room.Id}", room);
});

app.Run();