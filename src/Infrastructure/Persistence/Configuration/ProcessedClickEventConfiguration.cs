using Domain.Aggregates.ProcessedClick;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence.Configuration
{
    internal class ProcessedClickEventConfiguration : IEntityTypeConfiguration<ProcessedClickEvent>
    {
        public void Configure(
       EntityTypeBuilder<ProcessedClickEvent> builder)
        {
            builder.ToTable("ProcessedClickEvents");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.ShortLinkId)
                .IsRequired();

            builder.Property(x => x.ProcessedAt)
                .IsRequired();


            builder.HasIndex(x => x.ProcessedAt);
        }
    }
}
