﻿// <auto-generated />
using System;
using DiscordBot.Classes.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordBot.Migrations.RssDb
{
    [DbContext(typeof(RssDbContext))]
    [Migration("20230524202532_ArticleStats")]
    partial class ArticleStats
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("DiscordBot.Services.RssArticle", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("CustomId")
                        .HasColumnType("longtext");

                    b.Property<int>("FeedId")
                        .HasColumnType("int");

                    b.Property<bool>("IsImportant")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsRead")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Title")
                        .HasColumnType("longtext");

                    b.Property<string>("Url")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("FeedId");

                    b.ToTable("Articles");
                });

            modelBuilder.Entity("DiscordBot.Services.RssFeed", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("Interval")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<DateTime>("NextCheck")
                        .HasColumnType("datetime(6)");

                    b.Property<int?>("ParserId")
                        .HasColumnType("int");

                    b.Property<string>("Url")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("ParserId");

                    b.ToTable("Feeds");
                });

            modelBuilder.Entity("DiscordBot.Services.RssFeedFilterScript", b =>
                {
                    b.Property<int>("FeedId")
                        .HasColumnType("int");

                    b.Property<int>("FilterId")
                        .HasColumnType("int");

                    b.HasKey("FeedId", "FilterId");

                    b.HasIndex("FilterId");

                    b.ToTable("RssFeedFilterScript");
                });

            modelBuilder.Entity("DiscordBot.Services.RssScript", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Code")
                        .HasMaxLength(2147483647)
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("Scripts");
                });

            modelBuilder.Entity("DiscordBot.Services.RssArticle", b =>
                {
                    b.HasOne("DiscordBot.Services.RssFeed", null)
                        .WithMany("Articles")
                        .HasForeignKey("FeedId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("DiscordBot.Services.RssFeed", b =>
                {
                    b.HasOne("DiscordBot.Services.RssScript", "Parser")
                        .WithMany()
                        .HasForeignKey("ParserId");

                    b.Navigation("Parser");
                });

            modelBuilder.Entity("DiscordBot.Services.RssFeedFilterScript", b =>
                {
                    b.HasOne("DiscordBot.Services.RssFeed", "Feed")
                        .WithMany("Filters")
                        .HasForeignKey("FeedId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("DiscordBot.Services.RssScript", "Filter")
                        .WithMany()
                        .HasForeignKey("FilterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Feed");

                    b.Navigation("Filter");
                });

            modelBuilder.Entity("DiscordBot.Services.RssFeed", b =>
                {
                    b.Navigation("Articles");

                    b.Navigation("Filters");
                });
#pragma warning restore 612, 618
        }
    }
}
