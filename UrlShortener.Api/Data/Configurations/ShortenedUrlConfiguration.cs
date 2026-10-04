using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Data.Configurations;

public sealed class ShortenedUrlConfiguration : IEntityTypeConfiguration<ShortenedUrl>
{
    public void Configure(EntityTypeBuilder<ShortenedUrl> builder)
    {
        builder.ToTable("shortened_urls");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.ShortCode)
            .HasColumnName("short_code")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.OriginalUrl)
            .HasColumnName("original_url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(e => e.Alias)
            .HasColumnName("alias")
            .HasMaxLength(30);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.HasIndex(e => e.ShortCode)
            .IsUnique()
            .HasDatabaseName("ix_shortened_urls_short_code");

        builder.HasMany(e => e.ClickEvents)
            .WithOne(ce => ce.ShortenedUrl)
            .HasForeignKey(ce => ce.ShortCode)
            .HasPrincipalKey(e => e.ShortCode)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
