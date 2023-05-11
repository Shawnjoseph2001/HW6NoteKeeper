using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace HW6NoteKeeperSolution;

/// <summary>
/// Represents the database context for notes.
/// </summary>
public sealed class NoteContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the NoteContext class.
    /// </summary>
    /// <param name="options">The options to be used by the context.</param>
    public NoteContext(DbContextOptions<NoteContext> options): base(options)
    {
        Database.EnsureCreated();
    }
    /// <summary>
    /// Gets or sets the collection of notes.
    /// </summary>
    public DbSet<Note>? Notes {get; set; }
    /// <summary>
    /// Configures the model for notes.
    /// </summary>
    /// <param name="modelBuilder">The model builder to be used for configuration.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>().ToTable("Note");
    }
}

/// <summary>
/// This class represents a note with properties such as summary, description, created date and time,
/// modified date and time and a unique identifier.
/// </summary>
[Table("Note")]
public class Note
{
    /// <summary>
    /// Initializes a new instance of the Note class with default values.
    /// </summary>
    private Note()
    {
        id = Guid.NewGuid();
        summary = "Default Summary";
        details = "Default Description";
    }
    /// <summary>
    /// Gets the instance of the Note class.
    /// </summary>
    public static Note Instance { get; } = new();
    /// <summary>
    /// Gets or sets the summary of the note.
    /// </summary>
    [Required]
    [MinLength(1)]
    [StringLength(60)]
    public string summary { get; set; }
    /// <summary>
    /// Gets or sets the description of the note.
    /// </summary>
    ///
    [Required]
    [MinLength(1)]
    [StringLength(1024)]
    public string details { get; set; }
    /// <summary>
    /// The UTC time and date when the note was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDateUtc { get; set;}
    /// <summary>
    /// The UTC time and date when the note was last modified.
    /// </summary>
    [Required]
    public DateTimeOffset ModifiedDateUtc { get; set; }
    /// <summary>
    /// A unique identifier for the note.
    /// </summary>
    [Key]
    public Guid id { get; set; }
    /// <summary>
    /// Initializes a new instance of the Note class with specified summary, description and guid.
    /// </summary>
    /// <param name="summarySet">The summary of the note.</param>
    /// <param name="detailsSet">The description of the note.</param>
    /// <param name="idValue">The unique identifier for the note.</param>
    public Note(string summarySet, string detailsSet, Guid idValue)
    {
        summary = summarySet;
        details = detailsSet;
        CreatedDateUtc = DateTimeOffset.Now;
        ModifiedDateUtc = DateTimeOffset.MinValue;
        id = idValue;
    }
    /// <summary>
    /// Returns a JsonObject containing the note data including Summary, Description, NoteId, CreatedDateUtc and ModifiedDateUtc.
    /// </summary>
    /// <returns>A JsonObject containing the note data.</returns>

    public JsonObject GetNoteData()
    {
        // Create a JSON object with the note data.
        var returnValue = new JsonObject
        {
            ["Summary"] = summary,
            ["Description"] = details,
            ["NoteId"] = id.ToString(),
            ["CreatedDateUtc"] = CreatedDateUtc.UtcDateTime
        };
        // Deal with the possibility ModifiedDateUtc is supposed to be null
        if (ModifiedDateUtc == DateTimeOffset.MinValue)
        {
            returnValue["modifiedDateUtc"] = null;
        }
        else
        {
            returnValue["modifiedDateUtc"] = ModifiedDateUtc.UtcDateTime;
        }
        return returnValue;
    }
}