﻿// <auto-generated />
using System;
using DiscordBot.Services;
using DiscordBot.MLAPI.Modules.TimeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBot.Migrations.TimeTrackDbMigrations
{
    [DbContext(typeof(TimeTrackDb))]
    [Migration("20210525163919_AddIgnoreData")]
    partial class AddIgnoreData
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.0");

            modelBuilder.Entity("DiscordBot.MLAPI.Modules.TimeTracking.IgnoreData", b =>
                {
                    b.Property<long>("_userId")
                        .HasColumnType("bigint");

                    b.Property<string>("VideoId")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("_userId", "VideoId");

                    b.ToTable("Ignores");
                });

            modelBuilder.Entity("DiscordBot.MLAPI.Modules.TimeTracking.RedditData", b =>
                {
                    b.Property<long>("_userId")
                        .HasColumnType("bigint");

                    b.Property<string>("ThreadId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("Comments")
                        .HasColumnType("int");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("datetime2");

                    b.HasKey("_userId", "ThreadId");

                    b.ToTable("Threads");
                });

            modelBuilder.Entity("DiscordBot.MLAPI.Modules.TimeTracking.VideoData", b =>
                {
                    b.Property<long>("_userId")
                        .HasColumnType("bigint");

                    b.Property<string>("VideoId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("datetime2");

                    b.Property<double>("WatchedTime")
                        .HasColumnType("float");

                    b.HasKey("_userId", "VideoId");

                    b.ToTable("WatchTimes");
                });
#pragma warning restore 612, 618
        }
    }
}
