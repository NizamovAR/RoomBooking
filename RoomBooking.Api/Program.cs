using System.IO.Compression;
using System.Reflection.Metadata.Ecma335;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

//Проверка входных данных: возвращает null, если все в порядке
static string? Validate(string name, int capacity)
{
    if (string.IsNullOrWhiteSpace(name)) return "Name is required";
    if (name.Length > 100) return "Name is too long";
    if (capacity <= 0) return "Capacity must be positive";
    return null;
}

static RoomResponse ToResponse(Room room) => new(room.Id, room.Name, room.Capacity);

//общий префикс /rooms для эндпоинтов
var roomsGroup = app.MapGroup("/rooms");

//GET /rooms?page=1&pageSize=20
//AsNoTracking - для ускорения работы, просто чтение, без запоминания
roomsGroup.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 20) =>
{
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var items = await db.Rooms
        .AsNoTracking()
        .OrderBy(r => r.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(r => new RoomResponse(r.Id, r.Name, r.Capacity))
        .ToListAsync();

    return Results.Ok(items);
});

//GET /rooms/1
roomsGroup.MapGet("/{id}", async (int id, AppDbContext db) =>
{
    var room = await db.Rooms
        .AsNoTracking()
        .Where(r => r.Id == id)
        .Select(r => new RoomResponse(r.Id, r.Name, r.Capacity))
        .FirstOrDefaultAsync();

    return room is null ? Results.NotFound() : Results.Ok(room);
});


//POST /rooms
roomsGroup.MapPost("/", async (CreateRoomRequest request, AppDbContext db) =>
{
    var error = Validate(request.Name, request.Capacity);
    if(error is not null) return Results.BadRequest(error);
    var room = new Room { Name = request.Name, Capacity = request.Capacity };
    //Помечаем обьект как новый и готовый к добавлению
    db.Rooms.Add(room);
    //добавление данных в базу
    await db.SaveChangesAsync();

    return Results.Created($"/rooms/{room.Id}", room);
});

//PUT /rooms/6
roomsGroup.MapPut("/{id}", async (int id, UpdateRoomRequest request, AppDbContext db) =>
{
    var error = Validate(request.Name, request.Capacity);
    if (error is not null) return Results.BadRequest(error);

    var room = await db.Rooms.FindAsync(id);
    if (room is null) return Results.NotFound();

    room.Name = request.Name;
    room.Capacity = request.Capacity;
    await db.SaveChangesAsync();

    return Results.Ok(ToResponse(room));
});

//DELETE /rooms/6
roomsGroup.MapDelete("/{id}", async (int id, AppDbContext db) =>
{
    var room = await db.Rooms.FindAsync(id);
    if (room is null) return Results.NotFound();

    db.Rooms.Remove(room);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();