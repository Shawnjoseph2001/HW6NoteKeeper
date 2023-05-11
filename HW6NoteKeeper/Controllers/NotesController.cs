using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;

namespace HW6NoteKeeperSolution.Controllers;

[ApiController]
[Route("[controller]")]
public class NotesController : ControllerBase
{
    private readonly NoteContext _context;
    private readonly NotesSettings _settings;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<NotesController> _logger;
    private readonly TelemetryClient _telemetry;
    private readonly QueueClient _queueClient;
    /// <summary>
    /// Initializes a new instance of the NotesController class.
    /// </summary>
    /// <param name="context">The NoteContext object that represents the database context.</param>
    /// <param name="settings">The NotesSettings object that contains the application settings.</param>
    /// <param name="blobServiceClient">The BlobServiceClient object that represents the Azure Blob Storage service client.</param>
    /// <param name="logger">The ILogger object that provides logging functionality.</param>
    /// <param name="telemetry">The telemetry object for sending telemetry.</param>
    /// /// <param name="queueClient">The Azure queue client.</param>
    public NotesController(NoteContext context, NotesSettings settings, BlobServiceClient blobServiceClient, ILogger<NotesController> logger,
        TelemetryClient telemetry, QueueClient queueClient)
    {
        _queueClient = queueClient;
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        try
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "NoteContext: " + context + ", " + "NoteSettings: " + settings + ", BlobServiceClient: " + blobServiceClient + ", ILogger: " + logger + ", Telemetry: " + telemetry
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Add a new note to the database.
    /// </summary>
    /// <param name="inputData">A JSON object that contains the summary and details of the note.</param>
    /// <response code="201">Created- A new note has been created and added to the database.</response>
    /// <response code="400">Bad Request- The summary or details are missing, empty, or too long.</response>
    /// <response code="403">Forbidden- The maximum note limit has been reached, and no more notes can be added.</response>
    /// <returns>Returns a JSON object with the details of the newly created note and a location header.</returns>
    [HttpPost, Route("")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<object> PostAddNewNoteToDatabase([FromBody] NoteInput inputData)
    {
        try
        {
            // Check if the maximum note limit has been reached
            if (_settings.MaxNotes <= _context.Notes?.Count())
            {
                return new ProblemDetails
                {
                    Status = 403,
                    Title = "Note limit reached",
                    Detail = "Note limit reached MaxNotes: [" + _settings.MaxNotes + "]"
                };
            }

            // Validate the input data
            if (inputData.Summary == null || inputData.Details == null ||
                inputData.Summary.Length is < 1 or >= 60 ||
                inputData.Details.Length is < 1 or >= 1024)
            {
                // Return Bad Request if the input data is invalid
                _telemetry.TrackTrace("Failed to validate input data", new Dictionary<string, string>
                {
                    {"InputData", inputData.ToString() ?? string.Empty}
                });
                _telemetry.Flush();
                return BadRequest();
            }

            // Generate a new unique identifier for the note
            var guidString = Guid.NewGuid();
            Console.WriteLine("Creating a new GUID for the note.");
            Console.WriteLine("Creating a new note.");
            var createdNote = new Note(inputData.Summary, inputData.Details, guidString);
            _context.Notes?.Add(createdNote);
            await _context.SaveChangesAsync();
            // Return a 201 Created Result with the note data and location header
            return new CreatedResult($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/notes/{guidString}",
                createdNote.GetNoteData());
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload","NoteInputValues: " + inputData
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }

    /// <summary>
    /// Edit an existing note.
    /// </summary>
    /// <param name="noteInputValues">A JSON input with the note's summary and/or description.</param>
    /// <param name="noteId">The ID of the note to edit.</param>
    /// <response code="204">No Content- the note has been edited.</response>
    /// <response code="400">Bad Request- the summary or details are invalid, are empty, or are too long.</response>
    /// <response code="404">Not Found- The note could not be found.</response>
    [HttpPatch, Route("{noteId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public StatusCodeResult PatchEditExistingNote(Guid noteId, [FromBody] NoteInput noteInputValues)
    {
        try
        {
            // Find the note with the specified noteId
            var note = _context.Notes?.Find(noteId);
            // Check if the note exists
            if (note == null) return NotFound();
            // Update the summary if it is present in the input
            if (noteInputValues.Summary != null)
            {
                // Return Bad Request if summary is invalid

                if (noteInputValues.Summary.Length is < 1 or >= 60)
                {
                    _telemetry.TrackTrace("Summary length is invalid", new Dictionary<string, string>
                    {
                        {
                            "InputData", noteInputValues.ToString() ?? string.Empty
                        }
                    });
                    _telemetry.Flush();
                    return BadRequest();
                }
                note.summary = noteInputValues.Summary;
                note.ModifiedDateUtc = DateTimeOffset.Now;
            }

            // Update the description if it is present in the input
            if (noteInputValues.Details != null)
            {
                if (noteInputValues.Details.Length is < 1 or >= 1024)
                {
                    _telemetry.TrackTrace("Details length is invalid", new Dictionary<string, string>
                    {
                        {
                            "InputData", noteInputValues.ToString() ?? string.Empty
                        }
                    });
                    return BadRequest();
                }
                note.details = noteInputValues.Details;
                note.ModifiedDateUtc = DateTimeOffset.Now;
            }

            _context.SaveChanges();
            // Return a 204 result
            return NoContent();
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId + ", NoteInputValues: " + noteInputValues
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Get a note
    /// </summary>
    /// <param name="id">The ID of the note to edit.</param>
    /// <response code="200">Ok- Returns the details of the note.</response>
    /// <response code="404">Not Found- The note could not be found.</response>
    [HttpGet, Route("{id:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> GetOneNote(Guid id)
    {
        try
        {
            // Check if the note exists
            var note = await _context.Notes!.FindAsync(id);
            if (note == null)
            {
                // Return 404 Not Found if the note does not exist
                return new NotFoundResult();

            }

            return new OkObjectResult(note.GetNoteData());
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + id
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Get all existing notes.
    /// </summary>
    /// <response code="200">Ok- Returns the details of all notes.</response>
    [HttpGet, Route("")]
    public OkObjectResult GetAllExistingNotes()
    {
        try
        {
            // Return 200 OK and the details of all notes
            var notes = _context.Notes!.ToList();
            var noteDataList = notes.Select(note => note.GetNoteData()).ToList();
            return Ok(noteDataList);
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", ""
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Delete an existing note.
    /// </summary>
    /// <param name="noteId">The ID of the note to delete.</param>
    /// <response code="204">No Content- the note has been deleted.</response>
    /// <response code="404">Not Found- The note could not be found.</response>
    [HttpDelete("{noteId:guid}", Name = "notes")]
    public async Task<StatusCodeResult> DeleteOneNote(Guid noteId)
    {
        try
        {
            // Check if the note exists
            var note = await _context.Notes!.FindAsync(noteId);
            if (note == null)
            {
                // Return 404 Not Found if the note does not exist
                return NotFound();
            }

            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId.ToString());
            var blobZipContainerClient = _blobServiceClient.GetBlobContainerClient(noteId + "-zip");
            // Remove the note from the database and save changes
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            await foreach (var i in blobContainerClient.GetBlobsAsync())
            {
                var blobClient = blobContainerClient.GetBlobClient(i.Name);
                var deleteResult = await blobClient.DeleteIfExistsAsync();
                if (!deleteResult.Value)
                {
                    _logger.LogError("Failed to delete Azure blob" + i.Name);
                }
            }
            // Delete blob container
            var result = await blobContainerClient.DeleteIfExistsAsync();
            if (result.Value)
            {
                _logger.LogError("Failed to delete Azure container " + blobContainerClient.Name);
            }
            await foreach (var i in blobZipContainerClient.GetBlobsAsync())
            {
                var blobClient = blobContainerClient.GetBlobClient(i.Name);
                var deleteResult = await blobClient.DeleteIfExistsAsync();
                if (!deleteResult.Value)
                {
                    _logger.LogError("Failed to delete Azure blob" + i.Name);
                }
            }
            result = await blobZipContainerClient.DeleteIfExistsAsync();
            if (result.Value)
            {
                _logger.LogError("Failed to delete Azure container " + blobZipContainerClient.Name);
            }

            // Return 204 No Content
            return NoContent();
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }

    /// <summary>
    /// Add or update an attachment for an existing note.
    /// </summary>
    /// <param name="noteId">The ID of the note to attach the file to.</param>
    /// <param name="attachmentId">The ID of the attachment to update.</param>
    /// <param name="fileData">The file to upload.</param>
    /// <returns>The HTTP result.</returns>
    /// <response code="204">No Content- the attachment has been added or updated successfully.</response>
    /// <response code="400">Bad Request</response>
    /// <response code="403">Attachment Limit reached</response>
    /// <response code="404">Not Found- The note could not be found.</response>
    [HttpPut("{noteId:guid}/attachments/{attachmentId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<object> PutAttachment(Guid noteId, string attachmentId, IFormFile fileData)
    {
        try
        {
            if (attachmentId.Length == 0)
            {
                _telemetry.TrackTrace("Summary length is invalid", new Dictionary<string, string>
                {
                    {
                        "InputData", attachmentId
                    }
                });
                _telemetry.Flush();
                // Return bad request if the attachment ID is empty
                return BadRequest();
            }

            // Find the note with the specified noteId
            if (await _context.Notes!.FindAsync(noteId) == null)
            {
                // Return 404 Not Found if the note does not exist
                return NotFound();
            }

            // Get the container for the noteID
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId.ToString());
            // Create blob container client if it doesn't exist
            await blobContainerClient.CreateIfNotExistsAsync();
            // Set access policy to private
            await blobContainerClient.SetAccessPolicyAsync();
            // Make sure there are less than numAttachments attachments, 
            var numBlobs = 0;
            await foreach (var i in blobContainerClient.GetBlobsAsync())
            {
                // Exit the loop if we find the current attachment - we are not adding a new attachment
                if (i.Name == attachmentId)
                {
                    break;
                }

                // Increase the count of the number of blobs attached to the note
                numBlobs++;
                // Return an error if we exceed MaxAttachments
                if (numBlobs >= _settings.MaxAttachments)
                {
                    return new ProblemDetails
                    {
                        Status = 403,
                        Title = "Attachment limit reached",
                        Detail = "Attachment limit reached MaxAttachments: [" + _settings.MaxAttachments + "]"
                    };
                }
            }

            // Create a blob client for the blob to upload/replace
            var blobClient = blobContainerClient.GetBlobClient(attachmentId);
            // Check if the blob existed before upload
            var blobExists = await blobClient.ExistsAsync();
            // Write the blob to the URL
            await blobClient.UploadAsync(fileData.OpenReadStream(), overwrite: true);
            // Return a relevant HTTP response
            if (blobExists.Value)
            {
                // Track the telemetry event
                _telemetry.TrackEvent("AttachmentUpdated",
                    new Dictionary<string, string>
                    {
                        { "attachmentid", attachmentId }
                    }, new Dictionary<string, double>
                    {
                        { "AttachmentSize", fileData.Length }
                    });
                _telemetry.Flush();
                return NoContent();
            }

            // Track the telemetry event
            _telemetry.TrackEvent("AttachmentCreated",
                new Dictionary<string, string>
                {
                    { "attachmentid", attachmentId }
                }, new Dictionary<string, double>
                {
                    { "AttachmentSize", fileData.Length }
                });
            _telemetry.Flush();
            return Created($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/notes/{noteId}/attachments/" +
                           $"{attachmentId}", "");
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId + ", AttachmentID: " + attachmentId + ", " + "File data: " + fileData
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Delete an attachment
    /// </summary>
    /// <param name="noteId">The ID of the note to delete.</param>
    /// <param name="attachmentId">The attachment ID of the note to delete.</param>
    /// <returns>The HTTP result.</returns>
    /// <response code="204">No Content- the attachment has been added or updated successfully.</response>
    /// <response code="404">Not Found- The note could not be found.</response>
    /// /// <response code="500">Internal Server Error- The request failed.</response>
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [HttpDelete("{noteId:guid}/attachments/{attachmentId}")]
    public async Task<ActionResult> DeleteAttachment(Guid noteId, string attachmentId)
    {
        // Make sure the note exists
        try
        {
            if (await _context.Notes!.FindAsync(noteId) == null)
            {
                _logger.LogWarning("Note does not exist!");
                return NotFound();
            }

            // Get the blob container client
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId.ToString());
            // Make sure the blob container exists
            if (!await blobContainerClient.ExistsAsync())
            {
                _logger.LogWarning("Blob container client exists!");
                return NoContent();
            }

            // Get the blob client
            var blobClient = blobContainerClient.GetBlobClient(attachmentId);
            // Make sure the blob exists
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Blob client exists!");
                return NoContent();
            }

            // Delete the blob
            try
            {
                await blobClient.DeleteAsync();
            }
            // Catch failure, log, and return Problem()
            catch (RequestFailedException)
            {
                _logger.LogWarning("Request failed!");
                return Problem();
            }

            // Return 204 NoContent
            return NoContent();
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId + ", AttachmentID: " + attachmentId
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Get an attachment
    /// </summary>
    /// <param name="noteId">The ID of the note to delete.</param>
    /// <param name="attachmentId">The attachment ID of the note to delete.</param>
    /// <returns>The HTTP result.</returns>
    /// <response code="200">Ok- returns the file.</response>
    /// <response code="404">Not Found- The attachment could not be found.</response>
    [ProducesResponseType(200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [HttpGet("{noteId:guid}/attachments/{attachmentId}")]
    public async Task<ActionResult> GetAttachment(Guid noteId, string attachmentId)
    {
        try
        {
            // Make sure the note exists, return 404 if it doesn't
            if (await _context.Notes!.FindAsync(noteId) == null)
            {
                _logger.LogWarning("Failed to find " + noteId);
                return NotFound();
            }

            // Get the blob container client
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId.ToString());
            // Make sure the blob container exists
            if (!await blobContainerClient.ExistsAsync())
            {
                return NotFound();
            }

            // Get the blob and make sure it exists
            var blobClient = blobContainerClient.GetBlobClient(attachmentId);
            if (!await blobClient.ExistsAsync())
            {
                return NotFound();
            }

            // Get the blob from the server
            var result = await blobClient.DownloadStreamingAsync();
            // Create and return the data from the file stream
            return new FileStreamResult(result.Value.Content, result.Value.Details.ContentType)
            {
                FileDownloadName = attachmentId
            };
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId + ", AttachmentID: " + attachmentId
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Get all attachment info
    /// </summary>
    /// <param name="noteId">The ID of the note to get attachments about.</param>
    /// <returns>The HTTP result.</returns>
    /// <response code="200">Ok- returns the attachment info.</response>
    /// <response code="404">Not Found- The attachment could not be found.</response>
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [HttpGet("{noteId:guid}/attachments")]
    public async Task<object> GetAttachments(Guid noteId)
    {
        try
        {
            // Make sure note exists, log if doesn't
            if (await _context.Notes!.FindAsync(noteId) == null)
            {
                _logger.LogWarning("Failed to find " + noteId);
                return NotFound();
            }

            // Create blob container client
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId.ToString());
            // Create list of attachments to return
            var attachmentInfo = new List<JsonObject>();
            // Loop through blobs
            await foreach (var i in blobContainerClient.GetBlobsAsync())
            {
                if (i.Deleted) continue;
                // Turn blob info into JSON dict, return list of them
                attachmentInfo.Add(new JsonObject
                {
                    ["contentType"] = i.Properties.ContentType,
                    ["attachmentId"] = i.Name,
                    ["created"] = "{" + i.Properties.CreatedOn + "}" ?? throw new InvalidOperationException(),
                    ["lastModified"] = "{" + i.Properties.LastModified + "}" ?? throw new InvalidOperationException(),
                    ["length"] = i.Properties.ContentLength ?? throw new InvalidOperationException()
                });
            }

            return Ok(attachmentInfo);
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Create a ZIP file of all attachments
    /// </summary>
    /// <param name="noteId">The ID of the note to get the ZIP file attachment for.</param>
    /// <returns>The HTTP result.</returns>
    [HttpPost("{noteId:guid}/attachmentszipfiles")]
    public async Task<object> CreateAttachmentsZip(Guid noteId)
    {
        if (await _context.Notes!.FindAsync(noteId) == null)
        {
            _logger.LogWarning("Failed to find " + noteId);
            return NotFound();
        }
        var noteAttachment = new Dictionary<string, string>
        {
            ["noteId"] = noteId.ToString(),
            ["zipFileId"] = Guid.NewGuid() + ".zip"
        };
        await _queueClient.CreateIfNotExistsAsync();
        await _queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(noteAttachment))));
        var route =
            $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/notes/{noteId}/attachmentszipfiles/{noteAttachment["zipFileId"]}";
        return new AcceptedResult(route, "");
    }
    /// <summary>
    /// Delete the ZIP file of attachments
    /// </summary>
    /// <param name="noteId">The ID of the note to delete the ZIP attachment of.</param>
    /// <param name="attachmentId">The attachment ID of the note ZIP file to delete.</param>
    /// <returns>The HTTP result.</returns>
    [HttpDelete("{noteId:guid}/attachmentszipfiles/{attachmentId}")]
    public async Task<ActionResult> DeleteAttachmentZip(Guid noteId, string attachmentId)
    {
        // Make sure the note exists
        try
        {
            if (await _context.Notes!.FindAsync(noteId) == null)
            {
                _logger.LogWarning("Note does not exist!");
                return NotFound();
            }

            // Get the blob container client
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId + "-zip");
            // Make sure the blob container exists
            if (!await blobContainerClient.ExistsAsync())
            {
                _logger.LogWarning("Blob container client does not exist!");
                return NoContent();
            }
            // Get the blob client
            var blobClient = blobContainerClient.GetBlobClient(attachmentId);
            // Make sure the blob exists
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Blob client exists!");
                return NoContent();
            }
            // Delete the blob
            try
            {
                await blobClient.DeleteAsync();
            }
            // Catch failure, log, and return Problem()
            catch (RequestFailedException)
            {
                _logger.LogWarning("Request failed!");
                return Problem();
            }

            // Return 204 NoContent
            return NoContent();
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId + ", AttachmentID: " + attachmentId
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Get a ZIP file of all attachments
    /// </summary>
    /// <param name="noteId">The ID of the note to get the ZIP file attachment for.</param>
    /// <param name="attachmentId">The attachmenDet ID of the note ZIP file to get.</param>
    /// <returns>The HTTP result.</returns>
    [HttpGet("{noteId:guid}/attachmentszipfiles/{attachmentId}")]
    public async Task<ActionResult> GetAttachmentZip(Guid noteId, string attachmentId)
    {
        try
        {
            // Make sure the note exists, return 404 if it doesn't
            if (await _context.Notes!.FindAsync(noteId) == null)
            {
                _logger.LogWarning("Failed to find " + noteId);
                return NotFound();
            }

            // Get the blob container client
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId + "-zip");
            // Make sure the blob container exists
            if (!await blobContainerClient.ExistsAsync())
            {
                return NotFound();
            }
            // Get the blob and make sure it exists
            var blobClient = blobContainerClient.GetBlobClient(attachmentId);
            if (!await blobClient.ExistsAsync())
            {
                return NotFound();
            }

            // Get the blob from the server
            var result = await blobClient.DownloadStreamingAsync();
            // Create and return the data from the file stream
            return new FileStreamResult(result.Value.Content, contentType: "application/zip")
            {
                FileDownloadName = attachmentId
            };
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId + ", AttachmentID: " + attachmentId
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
    /// <summary>
    /// Get all attachment info
    /// </summary>
    /// <param name="noteId">The ID of the note to get attachments about.</param>
    /// <returns>The HTTP result.</returns>
    [HttpGet("{noteId:guid}/attachmentszipfiles/")]
    public async Task<object> GetAttachmentsZip(Guid noteId)
    {
        try
        {
            // Make sure note exists, log if doesn't
            if (await _context.Notes!.FindAsync(noteId) == null)
            {
                _logger.LogWarning("Failed to find " + noteId);
                return NotFound();
            }

            // Create blob container client
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(noteId + "-zip");
            // Create list of attachments to return
            var attachmentInfo = new List<JsonObject>();
            // Loop through blobs
            await foreach (var i in blobContainerClient.GetBlobsAsync())
            {
                if (i.Deleted) continue;
                // Turn blob info into JSON dict, return list of them
                attachmentInfo.Add(new JsonObject
                {
                    ["contentType"] = "application/zip",
                    ["attachmentId"] = i.Name,
                    ["created"] = "{" + i.Properties.CreatedOn + "}" ?? throw new InvalidOperationException(),
                    ["lastModified"] = "{" + i.Properties.LastModified + "}" ?? throw new InvalidOperationException(),
                    ["length"] = i.Properties.ContentLength ?? throw new InvalidOperationException()
                });
            }

            return Ok(attachmentInfo);
        }
        catch (Exception ex)
        {
            var properties = new Dictionary<string, string>
            {
                {
                    "InputPayload", "GUID: " + noteId
                }
            };
            _telemetry.TrackException(ex, properties);
            _telemetry.Flush();
            throw;
        }
    }
}