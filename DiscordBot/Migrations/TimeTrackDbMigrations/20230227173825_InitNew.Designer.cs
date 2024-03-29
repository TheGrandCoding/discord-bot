﻿// <auto-generated />
using System;
using DiscordBot.Classes.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordBot.Migrations.TimeTrackDbMigrations
{
    [DbContext(typeof(TimeTrackDb))]
    [Migration("20230227173825_InitNew")]
    partial class InitNew
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("MySql:CharSet", "utf8mb4")
                .HasAnnotation("ProductVersion", "7.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("DiscordBot.Services.IgnoreData", b =>
                {
                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.Property<string>("VideoId")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("UserId", "VideoId");

                    b.ToTable("Ignores");
                });

            modelBuilder.Entity("DiscordBot.Services.RedditData", b =>
                {
                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.Property<string>("ThreadId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("datetime2");

                    b.Property<int>("Comments")
                        .HasColumnType("int");

                    b.HasKey("UserId", "ThreadId", "LastUpdated");

                    b.ToTable("Threads");
                });

            modelBuilder.Entity("DiscordBot.Services.VideoData", b =>
                {
                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.Property<string>("VideoId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("datetime2");

                    b.Property<double>("WatchedTime")
                        .HasColumnType("float");

                    b.HasKey("UserId", "VideoId");

                    b.ToTable("WatchTimes");
                });
#pragma warning restore 612, 618
        }
    }
}
