using System.ComponentModel.DataAnnotations;

namespace MusicLibrary.Models;

public class Song
{
    public int Id { get; set; }

    public int ArtistId { get; set; }
    public Artist? Artist { get; set; }

    // A song may or may not be tied to a specific album.
    public int? AlbumId { get; set; }
    public Album? Album { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Track length in seconds (optional).</summary>
    public int? DurationSeconds { get; set; }

    /// <summary>Whether the user has marked this song as a favorite/loved.</summary>
    public bool IsFavorite { get; set; }
}
