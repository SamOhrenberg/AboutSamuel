﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PortfolioWebsite.Api.Data;

#nullable disable

namespace PortfolioWebsite.Api.Migrations
{
    [DbContext(typeof(SqlDbContext))]
    [Migration("20250209000903_InitialModel")]
    partial class InitialModel
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("PortfolioWebsite.Api.Data.Models.Information", b =>
                {
                    b.Property<Guid>("InformationId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Text")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("InformationId");

                    b.ToTable("Information");
                });

            modelBuilder.Entity("PortfolioWebsite.Api.Data.Models.Keyword", b =>
                {
                    b.Property<Guid>("KeywordId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("InformationId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("KeywordId");

                    b.HasIndex("InformationId");

                    b.ToTable("Keywords");
                });

            modelBuilder.Entity("PortfolioWebsite.Api.Data.Models.Keyword", b =>
                {
                    b.HasOne("PortfolioWebsite.Api.Data.Models.Information", "Information")
                        .WithMany("Keywords")
                        .HasForeignKey("InformationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Information");
                });

            modelBuilder.Entity("PortfolioWebsite.Api.Data.Models.Information", b =>
                {
                    b.Navigation("Keywords");
                });
#pragma warning restore 612, 618
        }
    }
}
