﻿// <auto-generated />
using System;
using DiscordBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordBot.Migrations.HoursDb
{
    [DbContext(typeof(HoursDbContext))]
    [Migration("20220801180558_AddBreaks")]
    partial class AddBreaks
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("DiscordBot.Services.HoursEntry", b =>
                {
                    b.Property<string>("SettingId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<double>("BreakHours")
                        .HasColumnType("float");

                    b.Property<double>("NormalHours")
                        .HasColumnType("float");

                    b.Property<double>("OvertimeHours")
                        .HasColumnType("float");

                    b.HasKey("SettingId", "UserId", "Date");

                    b.ToTable("Entries");
                });

            modelBuilder.Entity("DiscordBot.Services.HoursSettings", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("EndDate")
                        .HasColumnType("datetime2");

                    b.Property<double>("ExpectedBreak")
                        .HasColumnType("float");

                    b.Property<string>("ExpectedEndTime")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ExpectedStartTime")
                        .HasColumnType("nvarchar(max)");

                    b.Property<double>("NormalRate")
                        .HasColumnType("float");

                    b.Property<double>("OvertimeRate")
                        .HasColumnType("float");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("datetime2");

                    b.HasKey("Id", "UserId");

                    b.ToTable("Settings");
                });
#pragma warning restore 612, 618
        }
    }
}