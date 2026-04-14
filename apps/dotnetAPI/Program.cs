using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

//app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

var persons = new List<Person>
{
    new Person(12, "John", "Doe", 30, "john.doe@email.com"),
    new Person(143,"Jane", "Smith", 25, "jane.smith@email.com"),
    new Person(5, "Alice", "Johnson", 28, "ajohnson@domain.com"),
    new Person(356, "Bob", "Brown", 35, "bob_b@cybermail.com"),
};

var weatherDays = new[]
{
    new WeatherRecord(DateTime.Now.AddDays(1), 25, "Sunny"),
    new WeatherRecord(DateTime.Now.AddDays(2), 22, "Partly Cloudy"),
    new WeatherRecord(DateTime.Now.AddDays(3), 18, "Rainy"),
    new WeatherRecord(DateTime.Now.AddDays(4), 20, "Windy"),
    new WeatherRecord(DateTime.Now.AddDays(5), 23, "Sunny"),
};

var api = app.MapGroup("/api");
var healthApi = api.MapGroup("/health").WithTags("Health");

var personApi = api.MapGroup("/persons").WithTags("Person");
var weatherApi = api.MapGroup("/weather").WithTags("Weather");

healthApi.MapGet("/", () => Results.Ok(new { status = "Healthy" }))
.WithName("GetHealthStatus")
.Produces(StatusCodes.Status200OK);

personApi.MapGet("/", () => persons)
.WithName("GetPersons")
.Produces<List<Person>>(StatusCodes.Status200OK);
personApi.MapGet("/{id:int}", (int id) =>
{
    var person = persons.FirstOrDefault(p => p.id == id);
    return person is not null ? Results.Ok(person) : Results.NotFound();
})
.WithName("GetPersonByFirstName")
.Produces<Person>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);
personApi.MapPost("/", (Person person) =>
{
    if (persons.Any(p => p.id == person.id))
    {
        return Results.Conflict($"A person with ID {person.id} already exists.");
    }   
    persons.Add(person);
    return Results.Created($"/persons/{person.id}", person);
})
.WithName("CreatePerson")
.Produces<Person>(StatusCodes.Status201Created)
.Produces<string>(StatusCodes.Status409Conflict);

personApi.MapPut("/{id}", (int id, Person updatedPerson) =>
{
    var personIndex = persons.FindIndex(p => p.id == id);
    if (personIndex == -1)
    {
        return Results.NotFound();
    }
    persons[personIndex] = updatedPerson;
    return Results.NoContent();
})
.WithName("UpdatePerson")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);
personApi.MapDelete("/{id}", (int id) =>
{
    var personIndex = persons.FindIndex(p => p.id == id);
    if (personIndex == -1)
    {
        return Results.NotFound();
    }
    persons.RemoveAt(personIndex);
    return Results.NoContent();
})
.WithName("DeletePerson")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

weatherApi.MapGet("/", () => weatherDays)
.WithName("GetWeatherForecasts")
.Produces<List<WeatherRecord>>(StatusCodes.Status200OK);
weatherApi.MapGet("/{date}", (DateTime date) =>
{
    var record = weatherDays.FirstOrDefault(w => w.Date.Date == date.Date);
    return record is not null ? Results.Ok(record) : Results.NotFound();
})
.WithName("GetWeatherByDate")
.Produces<WeatherRecord>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.Run();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record Person (int id, string FirstName, string LastName, int Age, string Email);

record WeatherRecord(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}   
