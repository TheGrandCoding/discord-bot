﻿// <auto-generated />
using System;
using DiscordBot.Classes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DiscordBot.Migrations.BotDb
{
    [DbContext(typeof(BotDbContext))]
    [Migration("20230308203109_AddInsta")]
    partial class AddInsta
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("DiscordBot.Classes.BotDbApprovedIP", b =>
                {
                    b.Property<uint>("UserId")
                        .HasColumnType("int unsigned");

                    b.Property<string>("IP")
                        .HasColumnType("varchar(255)");

                    b.HasKey("UserId", "IP");

                    b.ToTable("BotDbApprovedIP");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbAuthSession", b =>
                {
                    b.Property<string>("Token")
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<bool>("Approved")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("IP")
                        .HasMaxLength(16)
                        .HasColumnType("varchar(16)");

                    b.Property<DateTime>("StartedAt")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(512)
                        .HasColumnType("varchar(512)");

                    b.Property<uint>("UserId")
                        .HasColumnType("int unsigned");

                    b.HasKey("Token");

                    b.HasIndex("UserId");

                    b.ToTable("AuthSessions");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbAuthToken", b =>
                {
                    b.Property<string>("Token")
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<string>("Name")
                        .HasMaxLength(32)
                        .HasColumnType("varchar(32)");

                    b.Property<uint>("UserId")
                        .HasColumnType("int unsigned");

                    b.HasKey("Token");

                    b.HasIndex("UserId");

                    b.ToTable("AuthTokens");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbPermission", b =>
                {
                    b.Property<uint>("UserId")
                        .HasColumnType("int unsigned");

                    b.Property<string>("PermNode")
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.HasKey("UserId", "PermNode");

                    b.ToTable("BotDbPermission");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbUser", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int unsigned");

                    b.Property<bool?>("Approved")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Name")
                        .HasMaxLength(32)
                        .HasColumnType("varchar(32)");

                    b.Property<string>("Reason")
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<string>("RedirectUrl")
                        .HasMaxLength(1024)
                        .HasColumnType("varchar(1024)");

                    b.Property<bool>("Verified")
                        .HasColumnType("tinyint(1)");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbApprovedIP", b =>
                {
                    b.HasOne("DiscordBot.Classes.BotDbUser", "User")
                        .WithMany("ApprovedIPs")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbAuthSession", b =>
                {
                    b.HasOne("DiscordBot.Classes.BotDbUser", "User")
                        .WithMany("AuthSessions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbAuthToken", b =>
                {
                    b.HasOne("DiscordBot.Classes.BotDbUser", "User")
                        .WithMany("AuthTokens")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbPermission", b =>
                {
                    b.HasOne("DiscordBot.Classes.BotDbUser", "User")
                        .WithMany("Permissions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbUser", b =>
                {
                    b.OwnsOne("DiscordBot.Classes.BotDbConnections", "Connections", b1 =>
                        {
                            b1.Property<uint>("BotDbUserId")
                                .HasColumnType("int unsigned");

                            b1.Property<string>("DiscordId")
                                .HasMaxLength(32)
                                .HasColumnType("varchar(32)");

                            b1.Property<string>("PasswordHash")
                                .HasMaxLength(128)
                                .HasColumnType("varchar(128)");

                            b1.HasKey("BotDbUserId");

                            b1.ToTable("Users");

                            b1.WithOwner()
                                .HasForeignKey("BotDbUserId");
                        });

                    b.OwnsOne("DiscordBot.Classes.BotDbInstagram", "Instagram", b1 =>
                        {
                            b1.Property<uint>("BotDbUserId")
                                .HasColumnType("int unsigned");

                            b1.Property<string>("AccessToken")
                                .HasColumnType("longtext");

                            b1.Property<string>("AccountId")
                                .HasColumnType("longtext");

                            b1.Property<DateTime>("ExpiresAt")
                                .HasColumnType("datetime(6)");

                            b1.HasKey("BotDbUserId");

                            b1.ToTable("Users");

                            b1.WithOwner()
                                .HasForeignKey("BotDbUserId");
                        });

                    b.OwnsOne("DiscordBot.Classes.BotDbUserOptions", "Options", b1 =>
                        {
                            b1.Property<uint>("BotDbUserId")
                                .HasColumnType("int unsigned");

                            b1.Property<int>("PairedVoiceChannels")
                                .HasColumnType("int");

                            b1.Property<int>("WhenToNotifyIsolation")
                                .HasColumnType("int");

                            b1.HasKey("BotDbUserId");

                            b1.ToTable("Users");

                            b1.WithOwner()
                                .HasForeignKey("BotDbUserId");
                        });

                    b.Navigation("Connections")
                        .IsRequired();

                    b.Navigation("Instagram");

                    b.Navigation("Options")
                        .IsRequired();
                });

            modelBuilder.Entity("DiscordBot.Classes.BotDbUser", b =>
                {
                    b.Navigation("ApprovedIPs");

                    b.Navigation("AuthSessions");

                    b.Navigation("AuthTokens");

                    b.Navigation("Permissions");
                });
#pragma warning restore 612, 618
        }
    }
}
