namespace HW6NoteKeeperSolution;
/// <summary>
/// This class represents the input data for creating a note through a REST API call.
/// </summary>
public class NoteInput
{
    /// <summary>
    /// Initializes a new instance of the NoteInput class with specified summary and details.
    /// </summary>
    /// <param name="summary">The summary of the note.</param>
    /// <param name="details">The details of the note.</param>
    public NoteInput(string? summary, string? details)
    {
        Summary = summary;
        Details = details;
    }
    /// <summary>
    /// A brief summary of the note.
    /// </summary>
    public string? Summary { get; }
    /// <summary>
    /// Detailed description of the note.
    /// </summary>
    public string? Details { get; }
}