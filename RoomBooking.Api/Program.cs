using System.IO.Compression;
using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

/* <--------------------------------Блок с валидацией------------------------------------------>
Надо будет перенести в отдельный файл
*/
//Проверка входных данных: возвращает null, если все в порядке
static string? Validate(string name, int capacity)
{
    if (string.IsNullOrWhiteSpace(name)) return "Name is required";
    if (name.Length > 100) return "Name is too long";
    if (capacity <= 0) return "Capacity must be positive";
    return null;
}

//Проверка входных данных для брони. Возвращает null, если все в порядке
static string? ValidateReservation(string userName, DateTimeOffset start, DateTimeOffset end)
{
    if (string.IsNullOrWhiteSpace(userName)) return "Username is required";
    if (userName.Length > 100) return "Username is too long";
    if (end <= start) return "End must be later then start";
    return null;
}

static RoomResponse ToResponse(Room room) =>
    new(room.Id, room.Name, room.Capacity);

static ReservationResponse ToReservationResponse(Reservation reservation) =>
    new(reservation.Id, reservation.RoomId, reservation.UserName, reservation.Start, reservation.End);

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
    if (error is not null) return Results.BadRequest(error);
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

    var hasReservations = await db.Reservations.AnyAsync(r => r.RoomId == id);
    if (hasReservations)
        return Results.Conflict("Room has reservations and cannot be deleted");

    db.Rooms.Remove(room);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

var reservationsGroup = app.MapGroup("/reservations");

//GET /reservations?roomId=1&page=1&pageSize=20
reservationsGroup.MapGet("/", async (AppDbContext db, int? roomId, int page = 1, int pageSize = 20) =>
{
    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var query = db.Reservations.AsNoTracking();

    //Фильр по комнатам. Необязательный
    if (roomId is not null)
        query = query.Where(r => r.RoomId == roomId);

    var items = await query
        .OrderBy(r => r.Start)
        .ThenByDescending(r => r.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(r => new ReservationResponse(r.Id, r.RoomId, r.UserName, r.Start, r.End))
        .ToListAsync();

    return Results.Ok(items);
});

//GET /reservations/5
reservationsGroup.MapGet("/{id}", async (int id, AppDbContext db) =>
{
    var reservation = await db.Reservations
        .AsNoTracking()
        .Where(r => r.Id == id)
        .Select(r => new ReservationResponse(r.Id, r.RoomId, r.UserName, r.Start, r.End))
        .FirstOrDefaultAsync();

    return reservation is null ? Results.NotFound() : Results.Ok(reservation);
});

//
//POST /reservations 
reservationsGroup.MapPost("/", async (CreateReservationRequest request, AppDbContext db) =>
{
    var error = ValidateReservation(request.UserName, request.Start, request.End);
    if (error is not null) return Results.BadRequest(error);

    var roomExists = await db.Rooms.AnyAsync(r => r.Id == request.RoomId);
    if (!roomExists) return Results.BadRequest($"Room {request.RoomId} does not exist");

    var reservation = new Reservation
    {
        RoomId = request.RoomId,
        UserName = request.UserName,
        Start = request.Start.UtcDateTime,
        End = request.End.UtcDateTime
    };

    db.Reservations.Add(reservation);
    await db.SaveChangesAsync();

    return Results.Created($"/reservations/{reservation.Id}", ToReservationResponse(reservation));
});

//DELETE /reservation/5
reservationsGroup.MapDelete("/{id}", async (int id, AppDbContext db) =>
{
    var reservation = await db.Reservations.FindAsync(id);
    if (reservation is null) return Results.NotFound();

    db.Reservations.Remove(reservation);
    await db.SaveChangesAsync();

    return Results.NoContent();

});

app.Run();