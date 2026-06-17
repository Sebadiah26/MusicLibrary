using Microsoft.EntityFrameworkCore;
using MusicLibrary.Models;

namespace MusicLibrary.Data;

public class MusicContext : DbContext
{
    public MusicContext(DbContextOptions<MusicContext> options) : base(options) { }

    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Song> Songs => Set<Song>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Treat artist name as the natural key for CSV de-duplication.
        b.Entity<Artist>()
            .HasIndex(a => a.Name)
            .IsUnique();

        b.Entity<Album>()
            .HasOne(al => al.Artist)
            .WithMany(a => a.Albums)
            .HasForeignKey(al => al.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Song>()
            .HasOne(s => s.Artist)
            .WithMany(a => a.Songs)
            .HasForeignKey(s => s.ArtistId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Song>()
            .HasOne(s => s.Album)
            .WithMany(al => al.Songs)
            .HasForeignKey(s => s.AlbumId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
