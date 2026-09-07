using Domain.Aggregates.ShortLinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configuration
{
    

    internal sealed class ShortLinkConfiguration
        : IEntityTypeConfiguration<ShortLink>
    {
        public void Configure(
            EntityTypeBuilder<ShortLink> builder)
        {
            builder.ToTable("ShortLinks");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.OriginalUrl)
                .HasMaxLength(2048)
                .IsRequired();

            builder.Property(x => x.ShortCode)
                .HasMaxLength(32)
                .IsRequired();

            builder.HasIndex(x => x.ShortCode)
                .IsUnique();

           

            builder.Property(x => x.ExpiresAt);

            builder.Property(x => x.IsActive)
                .IsRequired();
        }
    }
}
