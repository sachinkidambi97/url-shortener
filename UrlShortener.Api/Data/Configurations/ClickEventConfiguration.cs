using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Data.Configurations;

public sealed class ClickEventConfiguration : IEntityTypeConfiguration<ClickEvent>
{
    public void Configure(EntityTypeBuilder<ClickEvent> builder)
    {
        builder.ToTable("click_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.ShortCode)
            .HasColumnName("short_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.ClickedAt)
            .HasColumnName("clicked_at")
            .IsRequired();

        builder.Property(e => e.Referrer)
            .HasColumnName("referrer")
            .HasMaxLength(2048);

        builder.Property(e => e.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(512);

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.HasIndex(e => new { e.ShortCode, e.ClickedAt })
            .HasDatabaseName("ix_click_events_short_code_clicked_at");
    }
}
