using System.ComponentModel.DataAnnotations;

namespace MusicLibrary.Models;

public class Artist
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    // --- User-curated fields (set via the UI, not required in the CSV) ---

    [MaxLength(100)]
    public string? Genre { get; set; }

    [MaxLength(100)]
    public string? SubGenre { get; set; }

    public bool IsFavorite { get; set; }

    /// <summary>Rating from 1 to 5. Null means unrated.</summary>
    [Range(1, 5)]
    public int? Rating { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<Album> Albums { get; set; } = new();
    public List<Song> Songs { get; set; } = new();
}
