using System.ComponentModel.DataAnnotations;

namespace MusicLibrary.Models;

public class Album
{
    public int Id { get; set; }

    public int ArtistId { get; set; }
    public Artist? Artist { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public List<Song> Songs { get; set; } = new();
}
