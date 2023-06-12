﻿// <auto-generated />
using System;
using DiscordBot.Classes.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordBot.Migrations.FoodDb
{
    [DbContext(typeof(FoodDbContext))]
    [Migration("20220728192254_FixUses")]
    partial class FixUses
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("MySql:CharSet", "utf8mb4")
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);

            modelBuilder.Entity("DiscordBot.Services.HistoricItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<DateTime>("AddedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("InventoryId")
                        .HasColumnType("int");

                    b.Property<string>("ProductId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("RemovedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("PreviousInventory");
                });

            modelBuilder.Entity("DiscordBot.Services.InventoryItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"), 1L, 1);

                    b.Property<DateTime>("AddedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("Frozen")
                        .HasColumnType("bit");

                    b.Property<DateTime>("InitialExpiresAt")
                        .HasColumnType("datetime2")
                        .HasColumnName("ExpiresAt");

                    b.Property<string>("InventoryId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ProductId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("TimesUsed")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("ProductId");

                    b.ToTable("Inventory");
                });

            modelBuilder.Entity("DiscordBot.Services.Product", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<int?>("FreezingExtends")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Tags")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Uses")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(1);

                    b.HasKey("Id");

                    b.ToTable("Products");
                });

            modelBuilder.Entity("DiscordBot.Services.InventoryItem", b =>
                {
                    b.HasOne("DiscordBot.Services.Product", "Product")
                        .WithMany("InventoryItems")
                        .HasForeignKey("ProductId");

                    b.Navigation("Product");
                });

            modelBuilder.Entity("DiscordBot.Services.Product", b =>
                {
                    b.Navigation("InventoryItems");
                });
#pragma warning restore 612, 618
        }
    }
}
